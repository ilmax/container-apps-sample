using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProbeProtocol
{
    Http,
    Https,
    Tcp
}