using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker;

public record ProbeResult(
    TimeSpan Elapsed,
    DateTimeOffset ExecutedAtUtc,
    ProbeProtocol ProbeProtocol,
    bool Success,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorMessage);