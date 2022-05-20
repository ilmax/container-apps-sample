using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace Sample.Consumer.Messaging;

public class CustomSessionMessageProcessor : SessionMessageProcessor
{
    protected internal CustomSessionMessageProcessor(ServiceBusSessionProcessor processor)
        : base(processor)
    { }

    protected override Task<bool> BeginProcessingMessageAsync(ServiceBusSessionMessageActions actions, ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        message.InstrumentActivityFromMessage();

        return Task.FromResult(true);
    }
}