using AspNetMonsters.ApplicationInsights.AspNetCore;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Identity.Web;
using Sample.Producer.Communication;
using Sample.Producer.Config;

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
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"));
builder.Services.AddSingleton(
new ServiceBusClient(builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"), credentials));
builder.Services.AddSingleton<IServiceBusQueueSender, ServiceBusQueueSender>();
builder.Services.AddCloudRoleNameInitializer("Sample.Producer");
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

var app = builder.Build();

var ready = false;
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
timer.WaitForNextTickAsync().AsTask().ContinueWith(_ => ready = true);

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Hello");
app.MapGet("/healthz/liveness", () =>
{
    app.Logger.LogInformation("Liveness probe called");
    return "Alive";
});
app.MapGet("/healthz/startup", () =>
{
    app.Logger.LogInformation("Startup probe called");

    if (ready)
    {
        return Results.Ok("Alive v2");
    }

    return Results.BadRequest("Not yet ready");
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