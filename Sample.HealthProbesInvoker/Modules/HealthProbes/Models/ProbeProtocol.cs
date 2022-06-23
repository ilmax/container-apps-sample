using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProbeProtocol
{
    Http,
    Https,
    Tcp
}