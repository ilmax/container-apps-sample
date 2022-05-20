using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Sample.Consumer.Http;

namespace Sample.Consumer;

public class Processor
{
    private readonly DerivedClient _producerClient;
    private readonly ILogger<Processor> _logger;

    public Processor(DerivedClient producerClient, ILogger<Processor> logger)
    {
        _producerClient = producerClient;
        _logger = logger;
    }

    [FunctionName("OrderProcessor")]
    public async Task ProcessEvent(
        [ServiceBusTrigger("%QueueName%", Connection = "ServiceBusConnection", IsSessionsEnabled = false)]
        ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
    {
        try
        {
            _logger.LogInformation($"Starting to process message {message.MessageId}");

            var order = message.Body.ToObjectFromJson<Order>();

            if (Random.Shared.Next(0, 10) % 2 ==0)
            {
                var discount = await _producerClient.DiscountAsync(order.Id);

                _logger.LogInformation($"Discount for order {order.Id} is {discount.Amount}");
            }

            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing message {message.MessageId}");
        }
        finally
        {
            _logger.LogInformation($"Finished processing message {message.MessageId}");
        }
    }

    public class DerivedClient : ProducerClient
    {
        public DerivedClient(HttpClient httpClient)
            : base(httpClient.BaseAddress?.AbsoluteUri, httpClient)
        { }
    }
}