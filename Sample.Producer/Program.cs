using AspNetMonsters.ApplicationInsights.AspNetCore;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Sample.Producer.Communication;
using Sample.Producer.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var credentials = new DefaultAzureCredential(GetDefaultAzureCredentialOptions(builder.Environment));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"));
builder.Services.AddSingleton(
    new ServiceBusClient(builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"), credentials));
builder.Services.AddSingleton<IServiceBusQueueSender, ServiceBusQueueSender>();
builder.Services.AddCloudRoleNameInitializer("Sample.Producer");
builder.Services.AddHealthChecks()
    .AddAzureServiceBusQueue(
        builder.Configuration.GetSection("ServiceBus").GetValue<string>("Namespace"),
        builder.Configuration.GetSection("ServiceBus").GetValue<string>("Queue"),
        credentials);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();

app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();

static DefaultAzureCredentialOptions GetDefaultAzureCredentialOptions(IHostEnvironment hostEnvironment)
{
    return new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = hostEnvironment.IsDevelopment(),
        ExcludeInteractiveBrowserCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeSharedTokenCacheCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeVisualStudioCredential = !hostEnvironment.IsDevelopment(),
        ExcludeAzureCliCredential = !hostEnvironment.IsDevelopment(),
        ExcludeManagedIdentityCredential = hostEnvironment.IsDevelopment(),
    };
}