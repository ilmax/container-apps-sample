namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public class ProbeResultCollection
{
    private readonly List<ProbeResult> _probes = new();

    public void AddProbeResult(ProbeResult probeResult)
    {
        if (probeResult == null) throw new ArgumentNullException(nameof(probeResult));
        _probes.Add(probeResult);
    }

    public bool IsSuccessful => _probes.Count > 0 && _probes[^1].Success;

    public IEnumerable<ProbeResult> Probes => _probes;
}