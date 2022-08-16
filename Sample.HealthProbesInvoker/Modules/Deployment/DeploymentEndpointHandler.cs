using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;
using Sample.HealthProbesInvoker.Modules.Deployment.Models;
using Sample.HealthProbesInvoker.Modules.Deployment.Services;
using Sample.HealthProbesInvoker.Modules.HealthProbes.Models;
using Sample.HealthProbesInvoker.Modules.HealthProbes.Services;
using Sample.HealthProbesInvoker.Services;

namespace Sample.HealthProbesInvoker.Modules.Deployment;

public partial class DeploymentEndpointHandler
{
    private readonly ContainerAppProvider _containerAppProvider;
    private readonly RevisionManager _revisionManager;
    private readonly ProbeInvoker _prbInvoker;
    private readonly ILogger<DeploymentEndpointHandler> _logger;

    public DeploymentEndpointHandler(ContainerAppProvider containerAppProvider, RevisionManager revisionManager, ProbeInvoker prbInvoker, ILogger<DeploymentEndpointHandler> logger)
    {
        _containerAppProvider = containerAppProvider ?? throw new ArgumentNullException(nameof(containerAppProvider));
        _revisionManager = revisionManager ?? throw new ArgumentNullException(nameof(revisionManager));
        _prbInvoker = prbInvoker ?? throw new ArgumentNullException(nameof(prbInvoker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IResult> DeployNewImageAsync(string? rgName, string appName, string image)
    {
        Log.StartingDeployment(_logger, appName, image);

        ContainerAppResource? containerApp = null;
        ActiveRevisionsMode? initialRevisionMode = null;

        try
        {
            // Get the container app
            containerApp = await _containerAppProvider.GetContainerAppAsync(rgName, appName);

            initialRevisionMode = containerApp.Data.Configuration.ActiveRevisionsMode;

            // Set multiple revision mode
            await _revisionManager.SetMultipleRevisionsModeAsync(containerApp);

            // Redirect traffic to latest revision
            await _revisionManager.RedirectAllTrafficToRevisionAsync(containerApp, containerApp.Data.LatestRevisionName);

            // Deploy new revision with the given image
            var newRevisionName = await _revisionManager.DeployNewRevisionAsync(containerApp, image);

            // Get latest revision
            var latestRevision = await _containerAppProvider.GetRevisionAsync(containerApp, newRevisionName);

            // Warmups latest revision
            var warmupResult = await _prbInvoker.InvokeRevisionProbesAsync(latestRevision);

            // If warmup is successful
            switch (warmupResult)
            {
                case ProbeWarmup.Succeeded:
                    {
                        // Redirect traffic to latest revision
                        await _revisionManager.RedirectAllTrafficToRevisionAsync(containerApp, latestRevision.Data.Name);

                        // restore initial revision mode
                        await RestoreInitialRevisionMode(initialRevisionMode, containerApp);

                        Log.CompletedDeployment(_logger, latestRevision.Data.Name);

                        return Results.Ok(DeploymentResult.Succeeded(appName, latestRevision.Data.Name));
                    }
                case ProbeWarmup.PartiallySucceeded:
                    {
                        Log.WarmupPartiallySucceeded(_logger, appName, latestRevision.Data.Name);

                        return Results.BadRequest(DeploymentResult.PartiallySucceeded(appName, latestRevision.Data.Name));
                    }
                case ProbeWarmup.Failed:
                    {
                        Log.Failed(_logger, appName, latestRevision.Data.Name);

                        return Results.BadRequest(DeploymentResult.Failed(appName, latestRevision.Data.Name));
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(warmupResult));
            }
        }
        catch (Exception ex)
        {
            Log.DeploymentException(_logger, ex, appName);

            await RestoreInitialRevisionMode(initialRevisionMode, containerApp);

            return Results.BadRequest(DeploymentResult.Exception(ex.Message, appName));
        }
    }

    private async Task RestoreInitialRevisionMode(ActiveRevisionsMode? initialRevisionMode, ContainerAppResource? containerApp)
    {
        if (containerApp != null)
        {
            if (initialRevisionMode == ActiveRevisionsMode.Single)
            {
                await _revisionManager.SetSingleRevisionModeAsync(containerApp);
            }
            else if (initialRevisionMode == ActiveRevisionsMode.Multiple)
            {
                await _revisionManager.SetMultipleRevisionsModeAsync(containerApp);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 100,
            Level = LogLevel.Information,
            Message = "Starting deployment for app `{appName}` using the image `{image}`")]
        public static partial void StartingDeployment(ILogger logger, string appName, string image);

        [LoggerMessage(
            EventId = 101,
            Level = LogLevel.Information,
            Message = "Deployment completed successfully, revision `{revision}` is active and ready to serve traffic")]
        public static partial void CompletedDeployment(ILogger logger, string revision);

        [LoggerMessage(
            EventId = 102,
            Level = LogLevel.Warning,
            Message = "Some health probes returned a failure for app `{appName}`, revision `{revision}` has not been activated, please manually investigate the issue")]
        public static partial void WarmupPartiallySucceeded(ILogger logger, string appName, string revision);

        [LoggerMessage(
            EventId = 103,
            Level = LogLevel.Error,
            Message = "All health probes returned a failure for app `{appName}`, revision `{revision}` has not been activated, please manually investigate the issue")]
        public static partial void Failed(ILogger logger, string appName, string revision);

        [LoggerMessage(
            EventId = 104,
            Level = LogLevel.Error,
            Message = "Unhandled exception in while deploying app `{appName}`, please manually investigate the issue")]
        public static partial void DeploymentException(ILogger logger, Exception ex, string appName);
    }
}