using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

public record ProbeResult(
    TimeSpan Elapsed,
    DateTimeOffset ExecutedAtUtc,
    ProbeProtocol ProbeProtocol,
    bool Success,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorMessage);