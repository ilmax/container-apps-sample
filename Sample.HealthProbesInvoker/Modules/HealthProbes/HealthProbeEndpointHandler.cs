using Sample.HealthProbesInvoker.Modules.HealthProbes.Services;
using Sample.HealthProbesInvoker.Services;

namespace Sample.HealthProbesInvoker.Modules.HealthProbes;

public class HealthProbeEndpointHandler
{
    private readonly ContainerAppProvider _containerAppProvider;
    private readonly ProbeInvoker _prbInvoker;
    private readonly ILogger<HealthProbeEndpointHandler> _logger;

    public HealthProbeEndpointHandler(ContainerAppProvider containerAppProvider, ProbeInvoker prbInvoker, ILogger<HealthProbeEndpointHandler> logger)
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