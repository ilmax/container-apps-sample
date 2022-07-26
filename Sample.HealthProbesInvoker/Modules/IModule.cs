namespace Sample.HealthProbesInvoker.Modules;

public interface IModule
{
    void RegisterModule(WebApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder app);
}