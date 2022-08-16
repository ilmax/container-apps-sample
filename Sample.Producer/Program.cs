using System.Text.Json;
using AspNetMonsters.ApplicationInsights.AspNetCore;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sample.Producer.Communication;
using Sample.Producer.Config;
using ZNetCS.AspNetCore.IPFiltering;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var credentials = GetAzureCredentials(builder.Environment);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddSimpleConsole(opt =>
    {
        if (!builder.Environment.IsDevelopment())
        {
            opt.ColorBehavior = LoggerColorBehavior.Disabled;
            opt.SingleLine = true;
        }
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry(.);
builder.Services.Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"));
builder.Services.AddSingleton(
new ServiceBusClient(builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"), credentials));
builder.Services.AddSingleton<IServiceBusQueueSender, ServiceBusQueueSender>();
builder.Services.AddCloudRoleNameInitializer("Sample.Producer");
builder.Services.AddIPFiltering(builder.Configuration.GetSection("IPFiltering"));

var app = builder.Build();
app.UseIPFiltering();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();

app.MapControllers();
app.MapGet("/", () => "Hello");
app.MapGet("/healthz/liveness", () => "Alive");
app.MapGet("/healthz/startup", (IOptions<IPFilteringOptions> opt , ILogger<Program> logger) =>
{
    logger.LogWarning("IPFiltering options are: " + JsonSerializer.Serialize(opt));
    return "Started" + JsonSerializer.Serialize(opt);
});
app.Run();

static TokenCredential GetAzureCredentials(IHostEnvironment hostEnvironment)
{
    if (hostEnvironment.IsDevelopment())
    {
        return new DefaultAzureCredential();
    }

    return new ManagedIdentityCredential();
}