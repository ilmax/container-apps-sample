using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace Sample.Consumer.Messaging;

public class CustomMessageProcessor : MessageProcessor
{
    protected internal CustomMessageProcessor(ServiceBusProcessor processor)
        : base(processor)
    { }

    protected override Task<bool> BeginProcessingMessageAsync(ServiceBusMessageActions actions, ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        message.InstrumentActivityFromMessage();

        return Task.FromResult(true);
    }
}