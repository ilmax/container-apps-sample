using System.Collections;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public abstract class ProbeWarmup : IEnumerable<AggregateProbeResult>
{
    private readonly IEnumerable<AggregateProbeResult> _aggregateProbeResults;

    private protected  ProbeWarmup(IEnumerable<AggregateProbeResult> aggregateProbeResults)
    {
        _aggregateProbeResults = aggregateProbeResults;
    }

    public IEnumerator<AggregateProbeResult> GetEnumerator() => _aggregateProbeResults.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public class Succeeded : ProbeWarmup
    {
        internal Succeeded(IEnumerable<AggregateProbeResult> aggregateProbeResults) 
            : base(aggregateProbeResults)
        {  }
    }

    public class PartiallySucceeded : ProbeWarmup
    {
        internal PartiallySucceeded(IEnumerable<AggregateProbeResult> aggregateProbeResults) 
            : base(aggregateProbeResults)
        {  }
    }

    public class Failed : ProbeWarmup
    {
        internal Failed(IEnumerable<AggregateProbeResult> aggregateProbeResults) 
            : base(aggregateProbeResults)
        {  }
    }

    public static ProbeWarmup Create(List<AggregateProbeResult> aggregateProbeResults)
    {
        if (aggregateProbeResults.All(p => p.IsSuccessful))
        {
            return new Succeeded(aggregateProbeResults);
        }

        if (aggregateProbeResults.All(p => !p.IsSuccessful))
        {
            return new Failed(aggregateProbeResults);
        }

        return new PartiallySucceeded(aggregateProbeResults);
    }
}