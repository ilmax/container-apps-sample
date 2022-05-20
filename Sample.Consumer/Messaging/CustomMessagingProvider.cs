using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Options;

namespace Sample.Consumer.Messaging;

public class CustomMessagingProvider : MessagingProvider
{
    public CustomMessagingProvider(IOptions<ServiceBusOptions> options) : base(options)
    { }

    protected override MessageProcessor CreateMessageProcessor(ServiceBusClient client, string entityPath, ServiceBusProcessorOptions options)
    {
        return new CustomMessageProcessor(CreateProcessor(client, entityPath, options));
    }

    protected override SessionMessageProcessor CreateSessionMessageProcessor(ServiceBusClient client, string entityPath,
        ServiceBusSessionProcessorOptions options)
    {
        return new CustomSessionMessageProcessor(CreateSessionProcessor(client, entityPath, options));
    }
}