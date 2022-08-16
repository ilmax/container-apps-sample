using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;
using Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Services;

public partial class ProbeInvoker
{
    private readonly ILogger<ProbeInvoker> _logger;

    public ProbeInvoker(ILogger<ProbeInvoker> logger)
    {
        _logger = logger;
    }

    public async Task<ProbeWarmup> InvokeRevisionProbesAsync(ContainerAppRevisionResource containerAppRevisionResource)
    {
        ArgumentNullException.ThrowIfNull(containerAppRevisionResource);

        Log.EnsureRevisionIsProvisioned(_logger, containerAppRevisionResource.Data.Name);
        await EnsureRevisionIsProvisionedOrThrowAsync(containerAppRevisionResource);

        var containerAppRevision = containerAppRevisionResource.Data;

        var result = new List<AggregateProbeResult>(containerAppRevision.Template.Containers.Count);
        foreach (var containerAppContainer in containerAppRevision.Template.Containers)
        {
            result.Add(await WarmUpAsync(containerAppRevision, containerAppContainer));
        }

        return ProbeWarmup.Create(result);
    }

    private async Task EnsureRevisionIsProvisionedOrThrowAsync(ContainerAppRevisionResource containerAppRevisionResource)
    {
        // Fast path
        if (containerAppRevisionResource.Data.ProvisioningState == RevisionProvisioningState.Provisioned)
        {
            return;
        }

        if (containerAppRevisionResource.Data.ProvisioningState != RevisionProvisioningState.Provisioning)
        {
            Log.CanNotWarmupRevision(_logger, containerAppRevisionResource.Data.Name, containerAppRevisionResource.Data.HealthState);
            throw new InvalidOperationException("Revision is not in the provisioned state");
        }

        bool done;
        var iteration = 1;
        do
        {
            Log.WaitingForRevisionToBeProvisioned(_logger, containerAppRevisionResource.Data.Name, containerAppRevisionResource.Data.HealthState);
            await Task.Delay(TimeSpan.FromSeconds(3 * iteration));
            containerAppRevisionResource = await containerAppRevisionResource.GetAsync();
            done = containerAppRevisionResource.Data.ProvisioningState == RevisionProvisioningState.Provisioned;
        } while (!done && iteration++ < 6);

        if (!done)
        {
            Log.RevisionNotProvisioning(_logger, containerAppRevisionResource.Data.Name, containerAppRevisionResource.Data.HealthState);
            throw new InvalidOperationException("Revision is not in the provisioned state");
        }
    }

    private async Task<AggregateProbeResult> WarmUpAsync(ContainerAppRevisionData containerAppRevisionData, ContainerAppContainer container)
    {
        var result = new AggregateProbeResult();
        result.AddStartupResult(await InvokeProbeAsync(container.Probes, containerAppRevisionData, ProbeType.Startup));
        result.AddReadinessResult(await InvokeProbeAsync(container.Probes, containerAppRevisionData, ProbeType.Readiness));
        result.AddLivenessResult(await InvokeProbeAsync(container.Probes, containerAppRevisionData, ProbeType.Liveness));
        return result;
    }

    private async Task<ProbeResultCollection> InvokeProbeAsync(IList<ContainerAppProbe> containerAppProbes,
        ContainerAppRevisionData containerAppRevisionData, ProbeType probeType)
    {
        int probeExecution = 1;
        ProbeResultCollection probeResultCollection = new();

        var probe = containerAppProbes.SingleOrDefault(p => p.ProbeType == probeType);
        if (probe is not null)
        {
            Log.InvokingProbe(_logger, probeType.ToString(), containerAppRevisionData.Name);

            do
            {
                if (probe.HttpRequest is not null)
                {
                    probeResultCollection.AddProbeResult(
                        await InvokeHttpProbeAsync(containerAppRevisionData.Fqdn, probe.HttpRequest, probe.TimeoutSeconds, probeType.ToString()));
                }
                else
                {
                    probeResultCollection.AddProbeResult(
                        await InvokeTcpProbeAsync(containerAppRevisionData.Fqdn, probe.TcpSocketRequest, probe.TimeoutSeconds, probeType.ToString()));
                }
                if (!probeResultCollection.IsSuccessful)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            } while (!probeResultCollection.IsSuccessful || probeExecution++ > probe.FailureThreshold);

            Log.InvokedProbe(_logger, probeType.ToString(), containerAppRevisionData.Name, probeResultCollection.Status);

        }
        else
        {
            probeResultCollection.AddProbeResult(ProbeResult.Missing(probeType.ToString()));
        }
        return probeResultCollection;
    }

    private async Task<ProbeResult> InvokeTcpProbeAsync(string fqdn, TcpSocketRequestData requestData, int? probeTimeoutSeconds, string probeType)
    {
        var sw = Stopwatch.StartNew();
        var timeoutInMillis = CalculateTimeoutInMillis(probeTimeoutSeconds);
        string host = requestData.Host ?? fqdn;
        try
        {
            Log.ResolvingHostAddress(_logger, host);

            var ipAddress = await Dns.GetHostAddressesAsync(host);
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutInMillis);
            await client.ConnectAsync(ipAddress, requestData.Port, cts.Token);

            Log.TcpProbeSucceeded(_logger, host);
            return ProbeResult.Success(probeType, sw.Elapsed, ProbeProtocol.Tcp);
        }
        catch (OperationCanceledException oex)
        {
            Log.TcpProbeTimedOut(_logger, oex, host, timeoutInMillis);
            return ProbeResult.Timeout(probeType, sw.Elapsed, ProbeProtocol.Tcp, oex.Message);
        }
        catch (Exception ex)
        {
            Log.TcpProbeFailed(_logger, ex, host);
            return ProbeResult.Fail(probeType, sw.Elapsed, ProbeProtocol.Tcp, ex.Message);
        }
    }

    private async Task<ProbeResult> InvokeHttpProbeAsync(string fqdn, HttpRequestData requestData,
        int? probeTimeoutSeconds, string probeType)
    {
        using HttpClient client = new();
        var builder = new UriBuilder(fqdn)
        {
            Path = requestData.Path,
            Port = requestData.Port,
            Scheme = requestData.Scheme.HasValue ? requestData.Scheme.Value.ToString() : "https"
        };

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        if (requestData.HttpHeaders.Count > 0)
        {
            foreach (var header in requestData.HttpHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        var timeoutInMillis = CalculateTimeoutInMillis(probeTimeoutSeconds);
        using var cts = new CancellationTokenSource(timeoutInMillis);
        var sw = Stopwatch.StartNew();
        var probeProtocol = string.Equals("https", builder.Scheme, StringComparison.OrdinalIgnoreCase) ? ProbeProtocol.Https : ProbeProtocol.Http;

        try
        {
            var response = await client.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                Log.HttpProbeFailed(_logger, probeProtocol, response.StatusCode, await response.Content.ReadAsStringAsync(cts.Token));
                return ProbeResult.Fail(probeType, sw.Elapsed, probeProtocol, response.ReasonPhrase ?? "Unknown failure");
            }

            Log.HttpProbeSucceeded(_logger, probeProtocol, response.StatusCode);
            return ProbeResult.Success(probeType, sw.Elapsed, probeProtocol);
        }
        catch (OperationCanceledException oex)
        {
            Log.HttpProbeTimedOut(_logger, oex, probeProtocol, timeoutInMillis);
            return ProbeResult.Timeout(probeType, sw.Elapsed, ProbeProtocol.Tcp, oex.Message);
        }
        catch (Exception ex)
        {
            Log.HttpProbeException(_logger, ex, probeProtocol);
            return ProbeResult.Fail(probeType, sw.Elapsed, probeProtocol, ex.Message);
        }
    }

    private int CalculateTimeoutInMillis(int? probeTimeoutInSeconds)
    {
        const int defaultTimeoutInSeconds = 5;

        return (probeTimeoutInSeconds ?? defaultTimeoutInSeconds) * 1000;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 400,
            Level = LogLevel.Trace,
            Message = "Ensuring container app revision `{revision}` is provisioned")]
        public static partial void EnsureRevisionIsProvisioned(ILogger logger, string revision);

        [LoggerMessage(
            EventId = 401,
            Level = LogLevel.Error,
            Message = "Cannot warmup unhealthy revision `{revision}` in state `{state}`")]
        public static partial void CanNotWarmupRevision(ILogger logger, string revision, RevisionHealthState? state);

        [LoggerMessage(
            EventId = 402,
            Level = LogLevel.Warning,
            Message = "Waiting for container app revision `{revision}` to provision successfully, current state is `{state}`")]
        public static partial void WaitingForRevisionToBeProvisioned(ILogger logger, string revision, RevisionHealthState? state);

        [LoggerMessage(
            EventId = 403,
            Level = LogLevel.Error,
            Message = "Cannot warmup unhealthy revision `{revision}` in provision state {state} after all the retries")]
        public static partial void RevisionNotProvisioning(ILogger logger, string revision, RevisionHealthState? state);

        [LoggerMessage(
            EventId = 404,
            Level = LogLevel.Information,
            Message = "Invoking probe `{probe}` for revision `{revision}`")]
        public static partial void InvokingProbe(ILogger logger, string probe, string revision);

        [LoggerMessage(
            EventId = 405,
            Level = LogLevel.Trace,
            Message = "Completed invoking probe `{probe}` for revision `{revision}` with status `{status}`")]
        public static partial void InvokedProbe(ILogger logger, string probe, string revision, string status);

        [LoggerMessage(
            EventId = 406,
            Level = LogLevel.Warning,
            Message = "Execution of {protocol} probe failed with status code `{status}` and body `{body}`")]
        public static partial void HttpProbeFailed(ILogger logger, ProbeProtocol protocol, HttpStatusCode status, string body);

        [LoggerMessage(
            EventId = 407,
            Level = LogLevel.Trace,
            Message = "Execution of {protocol} probe succeeded with status code `{status}`")]
        public static partial void HttpProbeSucceeded(ILogger logger, ProbeProtocol protocol, HttpStatusCode status);

        [LoggerMessage(
            EventId = 408,
            Level = LogLevel.Warning,
            Message = "Execution of {protocol} probe timed out after {timeout}ms")]
        public static partial void HttpProbeTimedOut(ILogger logger, Exception ex, ProbeProtocol protocol, int timeout);

        [LoggerMessage(
            EventId = 409,
            Level = LogLevel.Warning,
            Message = "Execution of {protocol} probe failed with an exception")]
        public static partial void HttpProbeException(ILogger logger, Exception ex, ProbeProtocol protocol);

        [LoggerMessage(
            EventId = 410,
            Level = LogLevel.Trace,
            Message = "Resolving host address for `{host}`")]
        public static partial void ResolvingHostAddress(ILogger logger, string host);

        [LoggerMessage(
            EventId = 411,
            Level = LogLevel.Trace,
            Message = "Execution of tcp probe for `{host}` succeeded")]
        public static partial void TcpProbeSucceeded(ILogger logger, string host);

        [LoggerMessage(
            EventId = 412,
            Level = LogLevel.Warning,
            Message = "Execution of tcp probe for `{host}` timed out after {timeout}ms")]
        public static partial void TcpProbeTimedOut(ILogger logger, Exception ex, string host, int timeout);

        [LoggerMessage(
            EventId = 413,
            Level = LogLevel.Warning,
            Message = "Execution of tcp probe for `{host}` failed")]
        public static partial void TcpProbeFailed(ILogger logger, Exception ex, string host);
    }
}