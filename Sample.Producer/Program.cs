using AspNetMonsters.ApplicationInsights.AspNetCore;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Sample.Producer.Communication;
using Sample.Producer.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var credentials = GetAzureCredentials(builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();
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
app.Run();

static TokenCredential GetAzureCredentials(IHostEnvironment hostEnvironment)
{
    if (hostEnvironment.IsDevelopment())
    {
        return new DefaultAzureCredential();
    }

    return new ManagedIdentityCredential();
}