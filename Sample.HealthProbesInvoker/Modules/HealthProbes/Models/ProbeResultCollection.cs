namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public class ProbeResultCollection
{
    private readonly List<ProbeResult> _probes = new();

    public void AddProbeResult(ProbeResult probeResult)
    {
        if (probeResult == null) throw new ArgumentNullException(nameof(probeResult));
        _probes.Add(probeResult);
        IsSuccessful = probeResult.Succeeded;
    }

    //public bool IsSuccessful => _probes.Count > 0 && _probes[^1].Succeeded;
    public bool IsSuccessful { get; private set; }

    public string Status => IsSuccessful ? "Success" : "Failure";

    public IEnumerable<ProbeResult> Probes => _probes;
}