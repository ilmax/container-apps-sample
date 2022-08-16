using Sample.HealthProbesInvoker.Modules.Deployment.Services;

namespace Sample.HealthProbesInvoker.Modules.Deployment;

public class DeploymentModule : IModule
{
    public void RegisterModule(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<RevisionManager>();
        builder.Services.AddScoped<DeploymentEndpointHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("resource-groups/default/apps/{appName}/deploy", Endpoints.DeployNewImageCurrentRgAsync);
        app.MapGet("resource-groups/{rgName}/apps/{appName}/deploy", Endpoints.DeployNewImageAsync);
    }
}