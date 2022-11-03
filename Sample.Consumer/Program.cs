using Microsoft.Azure.WebJobs.ServiceBus;
using Sample.Consumer;
using Sample.Consumer.Messaging;

var builder = Host.CreateDefaultBuilder()
    .ConfigureWebJobs(b =>
    {
        b.AddServiceBus(options => { options.AutoCompleteMessages = false; });
    })
    .ConfigureHostConfiguration(configHost => configHost.AddEnvironmentVariables("ASPNETCORE_"))
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient<Processor.DerivedClient>(opt =>
        {
            opt.BaseAddress = new Uri(context.Configuration["ProducerBaseAddress"]);
        });
        services.AddSingleton<MessagingProvider, CustomMessagingProvider>();
    })
    .ConfigureLogging((context, logging) =>
    {
        string instrumentationKey = context.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
        if (string.IsNullOrEmpty(instrumentationKey))
        {
            throw new InvalidOperationException("Missing app insight key");
        }
        logging.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = instrumentationKey);
        logging.AddSimpleConsole();
    })
    .UseConsoleLifetime();

var host = builder.Build();
using (host)
{
    await host.RunAsync();
}