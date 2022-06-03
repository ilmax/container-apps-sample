using System.Text.Json.Serialization;

namespace Sample.HealthProbesInvoker;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProbeProtocol
{
    Http,
    Https,
    Tcp
}