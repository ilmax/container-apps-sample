﻿using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Sample.HealthProbesInvoker.Config;
using Sample.HealthProbesInvoker.Modules.HealthProbes.Services;
using Sample.HealthProbesInvoker.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes;

public class HealthProbeModule : IModule
{
    public void RegisterModule(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ContainerAppProvider>();
        builder.Services.AddScoped<ProbeInvoker>();
        builder.Services.AddScoped<HealthProbeEndpointHandler>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddScoped(_ => new ArmClient(GetAzureCredential(builder.Environment)));
        builder.Services.Configure<AzureConfig>(builder.Configuration.GetSection("Azure"));
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("warmup/{appName}", Endpoints.WarmupAppLatestRevisionAsync);
        app.MapGet("warmup/{appName}/revisions/{revisionName}", Endpoints.WarmupAppRevisionByNameAsync);
        app.MapGet("warmup/resourceGroups/{rgName}/apps/{appName}/revisions/{revisionName}", Endpoints.WarmupAppRevisionAsync);
    }

    private static TokenCredential GetAzureCredential(IHostEnvironment hostEnvironment)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return new DefaultAzureCredential();
        }
        return new ManagedIdentityCredential();
    }
}