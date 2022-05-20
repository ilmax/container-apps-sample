using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sample.Producer.Communication;
using Sample.Producer.Config;
using Sample.Producer.Models;

namespace Sample.Producer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IServiceBusQueueSender _serviceBusQueueSender;
        private readonly ServiceBusConfiguration _serviceBusConfiguration;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IServiceBusQueueSender serviceBusQueueSender, ServiceBusConfiguration serviceBusConfiguration, ILogger<OrdersController> logger)
        {
            _serviceBusQueueSender = serviceBusQueueSender;
            _serviceBusConfiguration = serviceBusConfiguration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create(Order order)
        {
            _logger.LogInformation("Creating order");

            Activity.Current?.AddBaggage("OrderId", Guid.NewGuid().ToString());

            // send the message
            await _serviceBusQueueSender.SendMessageAsync(_serviceBusConfiguration.Queue, order);

            _logger.LogInformation("Order created");

            return Ok();
        }

        [HttpGet("{id}/discount")]
        [ProducesResponseType(typeof(Discount), 200)]
        public Task<IActionResult> GetDiscount(int id)
        {
            _logger.LogInformation("Getting discount");
            var value = Random.Shared.Next(0, 10);
            if (value % 3 == 0)
            {
                throw new InvalidOperationException("Discount not found");
            }
            return Task.FromResult<IActionResult>(Ok(new Discount(id)));
        }
    }
}