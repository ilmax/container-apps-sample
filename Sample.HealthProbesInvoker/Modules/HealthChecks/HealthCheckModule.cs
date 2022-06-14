using Azure.Identity;
using Azure.ResourceManager;
using Sample.HealthProbesInvoker.Config;
using Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks;

public class HealthCheckModule : IModule
{
    public void RegisterModule(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<RevisionSelector>();
        builder.Services.AddScoped<ProbeInvoker>();
        builder.Services.AddScoped<EndpointHandler>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddScoped(_ => new ArmClient(new DefaultAzureCredential(GetDefaultAzureCredentialOptions(builder.Environment))));
        builder.Services.Configure<AzureConfig>(builder.Configuration.GetSection("Azure"));
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("warmup/{appName}", Endpoints.WarmupAppLatestRevisionAsync);
        app.MapGet("warmup/{appName}/revisions/{revisionName}", Endpoints.WarmupAppRevisionByNameAsync);
        app.MapGet("warmup/resourceGroups/{rgName}/apps/{appName}/revisions/{revisionName}", Endpoints.WarmupAppRevisionAsync);
    }

    private static DefaultAzureCredentialOptions GetDefaultAzureCredentialOptions(IHostEnvironment hostEnvironment)
    {
        return new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = !hostEnvironment.IsDevelopment(),
            ExcludeAzureCliCredential = !hostEnvironment.IsDevelopment(),
            ExcludeManagedIdentityCredential = hostEnvironment.IsDevelopment(),
        };
    }
}