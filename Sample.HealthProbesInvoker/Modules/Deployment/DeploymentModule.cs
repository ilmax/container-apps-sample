using Sample.HealthProbesInvoker.Modules.Deployment.Services;

namespace Sample.HealthProbesInvoker.Modules.Deployment;

public class DeploymentModule : IModule
{
    public void RegisterModule(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<TrafficManager>();
        builder.Services.AddScoped<DeploymentEndpointHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("apps/{appName}/deploy/{imageName}", Endpoints.DeployNewImageCurrentRgAsync);
        app.MapGet("resourceGroups/{rgName}/apps/{appName}/deploy/{imageName}", Endpoints.DeployNewImageAsync);
    }
}