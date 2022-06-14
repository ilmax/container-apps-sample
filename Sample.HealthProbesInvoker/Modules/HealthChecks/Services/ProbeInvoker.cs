using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;
using Sample.HealthProbesInvoker.Modules.HealthChecks.Models;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

public class ProbeInvoker
{
    private readonly ILogger<ProbeInvoker> _logger;

    public ProbeInvoker(ILogger<ProbeInvoker> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<AggregateProbeResult>> InvokeRevisionProbesAsync(ContainerAppRevisionResource containerAppRevisionResource)
    {
        if (containerAppRevisionResource == null)
        {
            throw new ArgumentNullException(nameof(containerAppRevisionResource));
        }

        // Ensure we wait for provisioning to succeed
        containerAppRevisionResource = await EnsureRevisionIsProvisionedAsync(containerAppRevisionResource);

        var containerAppRevision = containerAppRevisionResource.Data;

        if (containerAppRevisionResource.Data.HealthState != RevisionHealthState.Healthy)
        {
            _logger.LogWarning("Cannot warmup unhealthy revision in state {state}", containerAppRevisionResource.Data.HealthState);
            throw new InvalidOperationException("Cannot check health probes on an unhealthy revision");
        }

        var result = new List<AggregateProbeResult>();
        foreach (var containerAppContainer in containerAppRevision.Template.Containers)
        {
            result.Add(await WarmUpAsync(containerAppRevision, containerAppContainer));
        }

        return result;
    }

    private async Task<ContainerAppRevisionResource> EnsureRevisionIsProvisionedAsync(ContainerAppRevisionResource containerAppRevisionResource)
    {
        // Fast path
        if (containerAppRevisionResource.Data.ProvisioningState == RevisionProvisioningState.Provisioned)
        {
            return containerAppRevisionResource;
        }

        if (containerAppRevisionResource.Data.ProvisioningState != RevisionProvisioningState.Provisioning)
        {
            _logger.LogWarning("Cannot warmup unhealthy revision in provision state {state}", containerAppRevisionResource.Data.ProvisioningState);
            throw new InvalidOperationException("Revision is not in the provisioned state");
        }

        bool done;
        var iteration = 0;
        do
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            containerAppRevisionResource = await containerAppRevisionResource.GetAsync();
            done = containerAppRevisionResource.Data.ProvisioningState == RevisionProvisioningState.Provisioning;
        } while (!done || iteration++ < 5);

        if (!done)
        {
            _logger.LogWarning("Cannot warmup unhealthy revision in provision state {state} after all the retries", containerAppRevisionResource.Data.ProvisioningState);
            throw new InvalidOperationException("Revision is not in the provisioned state");
        }

        return containerAppRevisionResource;
    }

    private async Task<AggregateProbeResult> WarmUpAsync(ContainerAppRevisionData containerAppRevisionData, ContainerAppContainer container)
    {
        var result = new AggregateProbeResult();
        var startupProbe = container.Probes.SingleOrDefault(p => p.ProbeType == ProbeType.Startup);
        if (startupProbe is not null)
        {
            _logger.LogInformation("Starting to invoke Startup probe for revision {rn}", containerAppRevisionData.Name);
            var startupProbeResult = await CallProbe(containerAppRevisionData.Fqdn, startupProbe);
            result.AddStartupResult(startupProbeResult);
            _logger.LogInformation("Completed invoking Startup probe for revision {rn} with status {st}", containerAppRevisionData.Name, startupProbeResult.IsSuccessful);
        }

        var readinessProbe = container.Probes.SingleOrDefault(p => p.ProbeType == ProbeType.Readiness);
        if (readinessProbe is not null)
        {
            _logger.LogInformation("Starting to invoke Readiness probe for revision {rn}", containerAppRevisionData.Name);
            var readinessProbeResult = await CallProbe(containerAppRevisionData.Fqdn, readinessProbe);
            result.AddReadinessResult(readinessProbeResult);
            _logger.LogInformation("Completed invoking Readiness probe for revision {rn} with status {st}", containerAppRevisionData.Name, readinessProbeResult.IsSuccessful);
        }

        var livenessProbe = container.Probes.SingleOrDefault(p => p.ProbeType == ProbeType.Liveness);
        if (livenessProbe is not null)
        {
            _logger.LogInformation("Starting to invoke Liveness probe for revision {rn}", containerAppRevisionData.Name);
            var livenessProbeResult = await CallProbe(containerAppRevisionData.Fqdn, livenessProbe);
            result.AddLivenessResult(livenessProbeResult);
            _logger.LogInformation("Completed invoking Liveness probe for revision {rn} with status {st}", containerAppRevisionData.Name, livenessProbeResult.IsSuccessful);
        }

        return result;
    }

    private async Task<ProbeResultCollection> CallProbe(string fqdn, ContainerAppProbe probe)
    {
        int probeExecution = 1;

        ProbeResultCollection probeResultCollection = new();

        do
        {
            if (probe.HttpRequest is not null)
            {
                probeResultCollection.AddProbeResult(await CallHttpProbe(fqdn, probe.HttpRequest, probe.TimeoutSeconds));
            }
            else
            {
                probeResultCollection.AddProbeResult(await CallTcpProbe(fqdn, probe.TcpSocketRequest, probe.TimeoutSeconds));
            }
            if (!probeResultCollection.IsSuccessful)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        } while (!probeResultCollection.IsSuccessful || probeExecution++ > probe.FailureThreshold);

        return probeResultCollection;
    }

    private async Task<ProbeResult> CallTcpProbe(string fqdn, TcpSocketRequestData requestData, int? probeTimeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var ipAddress = await Dns.GetHostAddressesAsync(requestData.Host ?? fqdn);
            using TcpClient client = new TcpClient();
            using var cts = new CancellationTokenSource(GetTimeoutInMillis(probeTimeoutSeconds));
            await client.ConnectAsync(ipAddress, requestData.Port, cts.Token);
            sw.Stop();
            return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, ProbeProtocol.Tcp, true, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine(ex);
            return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, ProbeProtocol.Tcp, true, null);
        }
    }

    private async Task<ProbeResult> CallHttpProbe(string fqdn, HttpRequestData requestData, int? probeTimeoutSeconds)
    {
        HttpClient client = new HttpClient();
        var builder = new UriBuilder(fqdn);
        builder.Path = requestData.Path;
        builder.Port = requestData.Port;
        if (requestData.Scheme.HasValue)
        {
            builder.Scheme = requestData.Scheme.Value.ToString();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        if (requestData.HttpHeaders.Count > 0)
        {
            foreach (var header in requestData.HttpHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }
        using var cts = new CancellationTokenSource(GetTimeoutInMillis(probeTimeoutSeconds));
        var sw = Stopwatch.StartNew();
        var probeProtocol = string.Equals("https", builder.Scheme, StringComparison.OrdinalIgnoreCase) ? ProbeProtocol.Https : ProbeProtocol.Http;

        try
        {
            var response = await client.SendAsync(request, cts.Token);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, probeProtocol, false, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, probeProtocol, false, ex.Message);
        }

        return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, probeProtocol, true, null);
    }

    private int GetTimeoutInMillis(int? probeTimeoutInSeconds, [CallerMemberName] string method = "")
    {
        const int defaultTimeoutInSeconds = 5;

        var calculatedTimeout = (probeTimeoutInSeconds ?? defaultTimeoutInSeconds) * 1000;

        _logger.LogInformation("Using {timeout}ms for probe {probe}", calculatedTimeout, method);

        return calculatedTimeout;
    }
}