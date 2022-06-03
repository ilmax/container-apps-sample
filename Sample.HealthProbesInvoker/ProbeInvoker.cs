using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;

namespace Sample.HealthProbesInvoker;

public class ProbeInvoker
{
    private readonly ILogger<ProbeInvoker> _logger;

    public ProbeInvoker(ILogger<ProbeInvoker> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<AggregateProbeResult>> InvokeRevisionProbesAsync(ContainerAppRevisionData containerAppRevision)
    {
        if (containerAppRevision == null)
        {
            throw new ArgumentNullException(nameof(containerAppRevision));
        }
        if (containerAppRevision.HealthState != RevisionHealthState.Healthy)
        {
            throw new InvalidOperationException("Cannot check health probes on an unhealthy revision");
        }

        var result = new List<AggregateProbeResult>();
        foreach (var containerAppContainer in containerAppRevision.Template.Containers)
        {
            result.Add(await WarmUpAsync(containerAppRevision, containerAppContainer));
        }

        return result;
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
            _logger.LogInformation("Completed invoking Startup probe for revision {rn} with status {st}", containerAppRevisionData.Name, startupProbeResult);
        }

        var readinessProbe = container.Probes.SingleOrDefault(p => p.ProbeType == ProbeType.Readiness);
        if (readinessProbe is not null)
        {
            _logger.LogInformation("Starting to invoke Readiness probe for revision {rn}", containerAppRevisionData.Name);
            var readinessProbeResult = await CallProbe(containerAppRevisionData.Fqdn, readinessProbe);
            result.AddReadinessResult(readinessProbeResult);
            _logger.LogInformation("Completed invoking Readiness probe for revision {rn} with status {st}", containerAppRevisionData.Name, readinessProbeResult);
        }

        var livenessProbe = container.Probes.SingleOrDefault(p => p.ProbeType == ProbeType.Liveness);
        if (livenessProbe is not null)
        {
            _logger.LogInformation("Starting to invoke Liveness probe for revision {rn}", containerAppRevisionData.Name);
            var livenessProbeResult = await CallProbe(containerAppRevisionData.Fqdn, livenessProbe);
            result.AddLivenessResult(livenessProbeResult);
            _logger.LogInformation("Completed invoking Liveness probe for revision {rn} with status {st}", containerAppRevisionData.Name, livenessProbeResult);
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
            using var cts = new CancellationTokenSource(probeTimeoutSeconds * 1000 ?? 5000);
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
        using var cts = new CancellationTokenSource(probeTimeoutSeconds * 1000 ?? 5000);
        var sw = Stopwatch.StartNew();
        var response = await client.SendAsync(request, cts.Token);
        sw.Stop();
        var probeProtocol = string.Equals("https", builder.Scheme, StringComparison.OrdinalIgnoreCase) ? ProbeProtocol.Https : ProbeProtocol.Http;

        if (!response.IsSuccessStatusCode)
        {
            return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, probeProtocol, false, response.ReasonPhrase);
        }

        return new ProbeResult(sw.Elapsed, DateTimeOffset.UtcNow, probeProtocol, true, null);
    }
}