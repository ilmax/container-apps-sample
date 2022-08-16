using Azure;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;

namespace Sample.HealthProbesInvoker.Modules.Deployment.Services;

public partial class RevisionManager
{
    private readonly ILogger<RevisionManager> _logger;

    public RevisionManager(ILogger<RevisionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetSingleRevisionModeAsync(ContainerAppResource containerApp)
    {
        ArgumentNullException.ThrowIfNull(containerApp);

        Log.SettingSingleRevisionMode(_logger, containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        containerApp.Data.Configuration.ActiveRevisionsMode = ActiveRevisionsMode.Single;

        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));
        Log.SetSingleRevisionMode(_logger, containerApp.Data.Name);
    }

    public async Task SetMultipleRevisionsModeAsync(ContainerAppResource containerApp)
    {
        ArgumentNullException.ThrowIfNull(containerApp);

        Log.SettingMultipleRevisionMode(_logger, containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        containerApp.Data.Configuration.ActiveRevisionsMode = ActiveRevisionsMode.Multiple;

        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));
        Log.SetMultipleRevisionMode(_logger, containerApp.Data.Name);
    }

    public async Task RedirectAllTrafficToRevisionAsync(ContainerAppResource containerApp, string revisionName)
    {
        if (string.IsNullOrEmpty(revisionName))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(revisionName));
        }
        ArgumentNullException.ThrowIfNull(containerApp);

        Log.RedirectingTrafficToRevision(_logger, containerApp.Data.Name, revisionName);

        containerApp = await containerApp.GetAsync();
        CopyContainerSecrets(containerApp);

        var selectedTrafficRevision = containerApp.Data.Configuration.Ingress.Traffic.SingleOrDefault(tr => tr.RevisionName == revisionName);
        if (selectedTrafficRevision is null)
        {
            selectedTrafficRevision = new TrafficWeight
            {
                Weight = 0,
                RevisionName = revisionName
            };

            containerApp.Data.Configuration.Ingress.Traffic.Add(selectedTrafficRevision);
        }

        foreach (var trafficWeight in containerApp.Data.Configuration.Ingress.Traffic)
        {
            trafficWeight.Weight = 0;
        }

        selectedTrafficRevision.Weight = 100;

        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));

        Log.RedirectedTrafficToRevision(_logger, containerApp.Data.Name, revisionName);
    }

    public async Task<string> DeployNewRevisionAsync(ContainerAppResource containerApp, string image)
    {
        if (string.IsNullOrEmpty(image))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(image));
        }
        ArgumentNullException.ThrowIfNull(containerApp);

        Log.ProvisioningNewRevision(_logger, containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        var latestRevisionName = containerApp.Data.LatestRevisionName;

        // TODO: Find appropriate container by name instead of defaulting to the first container
        if (containerApp.Data.Template.Containers.Count == 0)
        {
            Log.NoContainerDefined(_logger, containerApp.Data.Name);
            throw new InvalidOperationException("No container found in the container app");
        }
        if (containerApp.Data.Template.Containers.Count > 1)
        {
            Log.TooManyContainerDefined(_logger, containerApp.Data.Name);
            throw new NotSupportedException("More than one container found in the container app, this operation is not yet supported");
        }
        containerApp.Data.Template.Containers[0].Image = image;

        // TODO: Do we need the RunAndRetry loop now?
        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));
        Log.ProvisionedRevision(_logger, containerApp.Data.Name);

        int delay = 0;
        do
        {
            await Task.Delay(delay);
            containerApp = await containerApp.GetAsync();
            delay += 1000;
            // TODO exit on failed revision deployment
        } while (containerApp.Data.LatestRevisionName == latestRevisionName);

        return containerApp.Data.LatestRevisionName;
    }

    private static async Task RunAnRetryAsync(Func<Task> operation)
    {
        while (true)
        {
            try
            {
                await operation();
                break;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static void CopyContainerSecrets(ContainerAppResource containerApp)
    {
        containerApp.Data.Configuration.Secrets.Clear();

        foreach (var secret in containerApp.GetSecrets().ToList())
        {
            containerApp.Data.Configuration.Secrets.Add(new AppSecret { Name = secret.Name, Value = secret.Value });
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 200,
            Level = LogLevel.Information,
            Message = "Setting app `{appName}` to single revisions mode")]
        public static partial void SettingSingleRevisionMode(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 201,
            Level = LogLevel.Trace,
            Message = "Set app `{appName}` to single revisions mode")]
        public static partial void SetSingleRevisionMode(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 202,
            Level = LogLevel.Information,
            Message = "Setting app `{appName}` to single revisions mode")]
        public static partial void SettingMultipleRevisionMode(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 203,
            Level = LogLevel.Trace,
            Message = "Set app `{appName}` to single revisions mode")]
        public static partial void SetMultipleRevisionMode(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 204,
            Level = LogLevel.Information,
            Message = "Redirecting all traffic for app `{appName}` to revision `{revision}`")]
        public static partial void RedirectingTrafficToRevision(ILogger logger, string appName, string revision);

        [LoggerMessage(
            EventId = 205,
            Level = LogLevel.Trace,
            Message = "Redirected all traffic for app `{appName}` to revision `{revision}`")]
        public static partial void RedirectedTrafficToRevision(ILogger logger, string appName, string revision);

        [LoggerMessage(
            EventId = 206,
            Level = LogLevel.Information,
            Message = "Provisioning a new revision for app `{appName}`")]
        public static partial void ProvisioningNewRevision(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 207,
            Level = LogLevel.Warning,
            Message = "Container app `{appName}` doesn't have any container configured")]
        public static partial void NoContainerDefined(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 208,
            Level = LogLevel.Warning,
            Message = "Container app `{appName}` has more than one container, this operation is not yet supported")]
        public static partial void TooManyContainerDefined(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 209,
            Level = LogLevel.Trace,
            Message = "Provisioned a new revision for app `{appName}`")]
        public static partial void ProvisionedRevision(ILogger logger, string appName);
    }
}