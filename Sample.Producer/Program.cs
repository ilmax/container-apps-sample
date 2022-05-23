using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Sample.Producer.Communication;
using Sample.Producer.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"));
Console.WriteLine("SB namespace:" + builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"));
builder.Services.AddSingleton(
    new ServiceBusClient(builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"), new DefaultAzureCredential()));
builder.Services.AddSingleton<IServiceBusQueueSender, ServiceBusQueueSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();