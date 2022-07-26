using Azure.ResourceManager.Applications.Containers.Models;
using Sample.HealthProbesInvoker.Modules.Deployment.Services;
using Sample.HealthProbesInvoker.Modules.HealthProbes.Services;
using Sample.HealthProbesInvoker.Services;

namespace Sample.HealthProbesInvoker.Modules.Deployment;

public class DeploymentEndpointHandler
{
    private readonly ContainerAppProvider _containerAppProvider;
    private readonly TrafficManager _trafficManager;
    private readonly ProbeInvoker _prbInvoker;
    private readonly ILogger<DeploymentEndpointHandler> _logger;

    public DeploymentEndpointHandler(ContainerAppProvider containerAppProvider, TrafficManager trafficManager, ProbeInvoker prbInvoker, ILogger<DeploymentEndpointHandler> logger)
    {
        _containerAppProvider = containerAppProvider ?? throw new ArgumentNullException(nameof(containerAppProvider));
        _trafficManager = trafficManager ?? throw new ArgumentNullException(nameof(trafficManager));
        _prbInvoker = prbInvoker ?? throw new ArgumentNullException(nameof(prbInvoker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IResult> DeployNewImageAsync(string? rgName, string appName, string image)
    {
        // Get the container app
        var containerApp = await _containerAppProvider.GetContainerAppAsync(rgName, appName);

        var initialRevisionMode = containerApp.Data.Configuration.ActiveRevisionsMode;

        try
        {
            // Set single revision mode
            await _trafficManager.SetSingleRevisionModeAsync(containerApp);

            // Redirect traffic to latest revision
            await _trafficManager.RedirectAllTrafficToRevisionAsync(containerApp, containerApp.Data.LatestRevisionName);

            // Set multiple revision mode
            await _trafficManager.SetMultipleRevisionsModeAsync(containerApp);

            // Deploy new image
            var newRevisionName = await _trafficManager.DeployNewRevisionAsync(containerApp, image);

            // Get latest revision
            var latestRevision = await _containerAppProvider.GetRevisionAsync(containerApp, newRevisionName);

            // Warmups latest revision
            var result = await _prbInvoker.InvokeRevisionProbesAsync(latestRevision);

            // Check if operation succeeded
            if (result.All(p => p.IsSuccessful))
            {
                // Redirect traffic to latest revision
                await _trafficManager.RedirectAllTrafficToRevisionAsync(containerApp, latestRevision.Data.Name);

                // Revert to single revision mode
                await _trafficManager.SetSingleRevisionModeAsync(containerApp);

                return Results.Ok(new
                {
                    status = $"revision {latestRevision.Data.Name} is active and ready to serve traffic",
                    revision = latestRevision.Data.Name
                });
            }
            else
            {
                _logger.LogWarning("Not all result are successful, returning a failure for {app}", appName);
                return Results.BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in while deploying a new image");
            if (initialRevisionMode == ActiveRevisionsMode.Single)
            {
                await _trafficManager.SetSingleRevisionModeAsync(containerApp);
            }
            else if (initialRevisionMode == ActiveRevisionsMode.Multiple)
            {
                await _trafficManager.SetMultipleRevisionsModeAsync(containerApp);
            }

            return Results.BadRequest(ex.Message);
        }
    }
}