using Azure;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Applications.Containers.Models;

namespace Sample.HealthProbesInvoker.Modules.Deployment.Services;

public class TrafficManager
{
    private readonly ILogger<TrafficManager> _logger;

    public TrafficManager(ILogger<TrafficManager> logger)
    {
        _logger = logger;
    }

    public async Task SetSingleRevisionModeAsync(ContainerAppResource containerApp)
    {
        if (containerApp == null)
        {
            throw new ArgumentNullException(nameof(containerApp));
        }
        _logger.LogInformation("Setting app {appName} to single revisions mode", containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        containerApp.Data.Configuration.ActiveRevisionsMode = ActiveRevisionsMode.Single;

        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));

        _logger.LogInformation("Set app {appName} to single revisions mode", containerApp.Data.Name);
    }

    public async Task SetMultipleRevisionsModeAsync(ContainerAppResource containerApp)
    {
        if (containerApp == null)
        {
            throw new ArgumentNullException(nameof(containerApp));
        }
        _logger.LogInformation("Setting app {appName} to multiple revisions mode", containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        containerApp.Data.Configuration.ActiveRevisionsMode = ActiveRevisionsMode.Multiple;

        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));

        _logger.LogInformation("Set app {appName} to multiple revisions mode", containerApp.Data.Name);
    }

    public async Task RedirectAllTrafficToRevisionAsync(ContainerAppResource containerApp, string revisionName)
    {
        if (containerApp == null)
        {
            throw new ArgumentNullException(nameof(containerApp));
        }
        _logger.LogInformation("Redirecting all traffic for app {appName} to revision {revName}", containerApp.Data.Name, revisionName);

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

        _logger.LogInformation("Set app {appName} to multiple revisions mode", containerApp.Data.Name);
    }

    public async Task<string> DeployNewRevisionAsync(ContainerAppResource containerApp, string image)
    {

        if (containerApp == null)
        {
            throw new ArgumentNullException(nameof(containerApp));
        }

        if (string.IsNullOrEmpty(image))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(image));
        }
        _logger.LogInformation("Provisioning a new revision for app {appName}", containerApp.Data.Name);

        CopyContainerSecrets(containerApp);
        var latestRevisionName = containerApp.Data.LatestRevisionName;

        if (containerApp.Data.Template.Containers.Count == 0)
        {
            _logger.LogWarning("Container app with name '{ca}' doesn't have any container configured", containerApp.Data.Name);
            throw new InvalidOperationException("No container found in the container app");
        }
        if (containerApp.Data.Template.Containers.Count > 1)
        {
            _logger.LogWarning("Container app with name '{ca}' has more than one container, unable to proceed", containerApp.Data.Name);
            throw new InvalidOperationException("More than one container found");
        }

        containerApp.Data.Template.Containers[0].Image = image;
        await RunAnRetryAsync(() => containerApp.UpdateAsync(WaitUntil.Completed, containerApp.Data));
        _logger.LogInformation("Provisioned a new revision for app {appName}", containerApp.Data.Name);

        int delay = 0;
        do
        {
            await Task.Delay(delay);
            containerApp = await containerApp.GetAsync();
            delay += 1000;
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
}