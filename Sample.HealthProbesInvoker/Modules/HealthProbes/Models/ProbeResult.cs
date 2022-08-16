namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public abstract record ProbeResult
{
    private protected ProbeResult(string probeType, bool succeeded, DateTimeOffset executedAtUtc)
    {
        ProbeType = probeType;
        Succeeded = succeeded;
        ExecutedAtUtc = executedAtUtc;
    }

    public string ProbeType { get; }
    public bool Succeeded { get; }
    public DateTimeOffset ExecutedAtUtc { get; }

    public record TimeoutResult : ProbeResult
    {
        internal TimeoutResult(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol, string error) 
            : base(probeType, false, DateTimeOffset.UtcNow)
        {
            Elapsed = elapsed;
            ProbeProtocol = probeProtocol;
            Error = error;
            ÌsTimeout = true;
        }

        public TimeSpan Elapsed { get; }
        public ProbeProtocol ProbeProtocol { get; }
        public string Error { get; }
        public bool ÌsTimeout { get; }
    }

    public record FailResult : ProbeResult
    {
        internal FailResult(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol, string error)
            : base(probeType, false, DateTimeOffset.UtcNow)
        {
            Elapsed = elapsed;
            ProbeProtocol = probeProtocol;
            Error = error;
        }

        public TimeSpan Elapsed { get; }
        public ProbeProtocol ProbeProtocol { get; }
        public string Error { get; }
    }

    public record MissingResult : ProbeResult
    {
        internal MissingResult(string probeType)
            : base(probeType, true, DateTimeOffset.UtcNow)
        {
            ÌsMissing = true;
        }

        public bool ÌsMissing { get; }
    }

    public record SuccessResult : ProbeResult
    {
        internal SuccessResult(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol) 
            : base(probeType, true, DateTimeOffset.UtcNow)
        {
            Elapsed = elapsed;
            ProbeProtocol = probeProtocol;
        }

        public TimeSpan Elapsed { get; }
        public ProbeProtocol ProbeProtocol { get; }
    }

    public static ProbeResult Fail(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol, string error) =>
        new FailResult(probeType, elapsed, probeProtocol, error);

    public static ProbeResult Success(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol) =>
        new SuccessResult(probeType, elapsed, probeProtocol);

    public static ProbeResult Timeout(string probeType, TimeSpan elapsed, ProbeProtocol probeProtocol, string error) =>
        new TimeoutResult(probeType, elapsed, probeProtocol, error);

    public static ProbeResult Missing(string probeType) =>
        new MissingResult(probeType);
}