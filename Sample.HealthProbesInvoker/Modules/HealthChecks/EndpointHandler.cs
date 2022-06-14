using Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks;

public class EndpointHandler
{
    private readonly ContainerAppProvider _containerAppProvider;
    private readonly ProbeInvoker _prbInvoker;
    private readonly ILogger<EndpointHandler> _logger;

    public EndpointHandler(ContainerAppProvider containerAppProvider, ProbeInvoker prbInvoker, ILogger<EndpointHandler> logger)
    {
        _containerAppProvider = containerAppProvider ?? throw new ArgumentNullException(nameof(containerAppProvider));
        _prbInvoker = prbInvoker ?? throw new ArgumentNullException(nameof(prbInvoker));
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(string? rgName, string appName, string? revName)
    {
        try
        {
            var revision = await _containerAppProvider.GetRevisionAsync(rgName, appName, revName);

            var result = await _prbInvoker.InvokeRevisionProbesAsync(revision);

            if (result.All(p => p.IsSuccessful))
            {
                return Results.Ok(new
                {
                    status = $"revision {revision.Data.Name} is active and ready to serve traffic",
                    revision = revision.Data.Name
                });
            }

            _logger.LogWarning("Not all result are successful, returning a failure for {app}", appName);
            return Results.BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to warmup application {app}", appName);
            return Results.BadRequest(ex.Message);
        }
    }
}