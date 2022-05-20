using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace Sample.Producer.Communication;
public interface IServiceBusQueueSender
{
    Task SendMessageAsync<TPayload>(string queueName, TPayload message, CancellationToken cancellationToken = default)
        where TPayload : notnull;
}

public class ServiceBusQueueSender : IAsyncDisposable, IServiceBusQueueSender
{
    private readonly ServiceBusClient _serviceBusClient;

    public ServiceBusQueueSender(ServiceBusClient serviceBusClient)
    {
        _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
    }

    public async Task SendMessageAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default) where TMessage : notnull
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(queueName));
        }

        await using ServiceBusSender sender = _serviceBusClient.CreateSender(queueName);

        var serviceBusMessage = new ServiceBusMessage(new BinaryData(message));

        // Courtesy of https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Microsoft.Azure.ServiceBus/src/ServiceBusDiagnosticsSource.cs#L664-L692
        // This was the old service bus SDK that was used to carry along the current activity baggage, the new one is not doing it so we need to roll our own
        var currentActivity = Activity.Current;
        if (currentActivity?.Baggage is not null)
        {
            serviceBusMessage.ApplicationProperties["Correlation-Context"] = string.Join(",", currentActivity.Baggage.Select(kvp => kvp.Key + "=" + kvp.Value));
        }

        // send the message
        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public ValueTask DisposeAsync() => _serviceBusClient.DisposeAsync();
}