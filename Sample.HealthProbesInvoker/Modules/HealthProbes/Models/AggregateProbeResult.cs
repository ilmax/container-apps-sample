using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public class AggregateProbeResult
{
    public ProbeResultCollection? Startup { get; private set; }
    public ProbeResultCollection? Readiness { get; private set; }
    public ProbeResultCollection? Liveness { get; private set; }

    [JsonIgnore]
    public bool IsSuccessful { get; private set; }

    public void AddStartupResult(ProbeResultCollection probeResultCollection)
    {
        Startup = probeResultCollection ?? throw new ArgumentNullException(nameof(probeResultCollection));
        IsSuccessful = probeResultCollection.IsSuccessful;
    }

    public void AddReadinessResult(ProbeResultCollection probeResultCollection)
    {
        Readiness = probeResultCollection ?? throw new ArgumentNullException(nameof(probeResultCollection));
        IsSuccessful = probeResultCollection.IsSuccessful;
    }

    public void AddLivenessResult(ProbeResultCollection probeResultCollection)
    {
        Liveness = probeResultCollection ?? throw new ArgumentNullException(nameof(probeResultCollection));
        IsSuccessful = probeResultCollection.IsSuccessful;
    }
}