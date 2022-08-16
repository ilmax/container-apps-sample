using Azure.ResourceManager;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using Sample.HealthProbesInvoker.Config;

namespace Sample.HealthProbesInvoker.Services;

public partial class ContainerAppProvider
{
    private readonly ArmClient _client;
    private readonly AzureConfig _azureConfig;
    private readonly ILogger<ContainerAppProvider> _logger;

    public ContainerAppProvider(ArmClient client, IOptions<AzureConfig> azureConfig, ILogger<ContainerAppProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _azureConfig = azureConfig.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ContainerAppResource> GetContainerAppAsync(string? resourceGroupName, string applicationName)
    {
        if (string.IsNullOrEmpty(applicationName))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(applicationName));
        }

        Log.GettingContainerApp(_logger, applicationName);
        SubscriptionResource subscription = await _client.GetSubscriptions().GetAsync(_azureConfig.SubscriptionId);

        if (subscription is null)
        {
            Log.SubscriptionNotFound(_logger);
            throw new InvalidOperationException("Subscription not found");
        }

        ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName ?? _azureConfig.ResourceGroupName);
        if (resourceGroup is null)
        {
            Log.ResourceGroupNotFound(_logger, resourceGroupName ?? _azureConfig.ResourceGroupName, subscription.Data.DisplayName);
            throw new InvalidOperationException("Resource group not found");
        }

        ContainerAppResource containerApp = await resourceGroup.GetContainerAppAsync(applicationName);
        if (containerApp is null)
        {
            Log.ContainerAppNotFound(_logger, applicationName, resourceGroup.Data.Name);
            throw new InvalidOperationException("Container app not found");
        }

        Log.GotContainerApp(_logger, applicationName);

        return containerApp;
    }

    public async Task<ContainerAppRevisionResource> GetRevisionAsync(string? resourceGroupName, string applicationName, string? revisionName)
    {
        if (string.IsNullOrEmpty(applicationName))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(applicationName));
        }

        ContainerAppResource containerApp = await GetContainerAppAsync(resourceGroupName, applicationName);

        return await GetRevisionAsync(containerApp, revisionName);
    }

    public async Task<ContainerAppRevisionResource> GetRevisionAsync(ContainerAppResource containerApp, string? revisionName)
    {
        ArgumentNullException.ThrowIfNull(containerApp);

        if (containerApp.Data.Configuration.Ingress is null)
        {
            Log.ContainerAppDoesNotHaveIngress(_logger, containerApp.Data.Name);
            throw new InvalidOperationException("Container app not doesn't have an ingress");
        }

        return !string.IsNullOrEmpty(revisionName)
            ? await GetRevisionResourceByNameAsync(containerApp, revisionName)
            : await GetLatestRevisionAsync(containerApp);
    }

    public async Task<ContainerAppRevisionResource> GetRevisionResourceByNameAsync(ContainerAppResource containerApp, string revisionName)
    {
        ArgumentNullException.ThrowIfNull(containerApp);

        ContainerAppRevisionResource revision = await containerApp.GetContainerAppRevisions().GetAsync(revisionName);
        if (revision is null)
        {
            Log.RevisionNotFound(_logger, containerApp.Data.Name, revisionName);
            throw new InvalidOperationException("Container app not doesn't have the requested revision");
        }

        Log.GotContainerAppRevision(_logger, containerApp.Data.Name, revisionName);
        return revision;
    }

    private Task<ContainerAppRevisionResource> GetLatestRevisionAsync(ContainerAppResource containerApp)
    {
        return GetRevisionResourceByNameAsync(containerApp, containerApp.Data.LatestRevisionName);
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 300,
            Level = LogLevel.Information,
            Message = "Getting container app `{appName}`")]
        public static partial void GettingContainerApp(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 301,
            Level = LogLevel.Error,
            Message = "Unable to get the current subscription, ensure the application can get access to azure via az cli or Managed Service Identity and the Azure:SubscriptionId configuration is set")]
        public static partial void SubscriptionNotFound(ILogger logger);

        [LoggerMessage(
            EventId = 302,
            Level = LogLevel.Error,
            Message = "Unable to find a resource group with name `{resourceGroup}`, ensure the name is correct and the resource group exists in the current subscription `{subscriptionName}`")]
        public static partial void ResourceGroupNotFound(ILogger logger, string resourceGroup, string subscriptionName);

        [LoggerMessage(
            EventId = 303,
            Level = LogLevel.Error,
            Message = "Unable to find a container app with name `{appName}`, in resource group `{resourceGroup}`, ensure that the resource group is correct")]
        public static partial void ContainerAppNotFound(ILogger logger, string appName, string resourceGroup);

        [LoggerMessage(
            EventId = 304,
            Level = LogLevel.Trace,
            Message = "Got container app `{appName}`")]
        public static partial void GotContainerApp(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 305,
            Level = LogLevel.Error,
            Message = "Container app with name `{appName}` doesn't have an ingress, only container app with an ingress are supported")]
        public static partial void ContainerAppDoesNotHaveIngress(ILogger logger, string appName);

        [LoggerMessage(
            EventId = 306,
            Level = LogLevel.Error,
            Message = "Container app with name `{appName}` doesn't have a revision named `{revision}`")]
        public static partial void RevisionNotFound(ILogger logger, string appName, string revision);

        [LoggerMessage(
            EventId = 307,
            Level = LogLevel.Trace,
            Message = "Got container app `{appName}` revision `{revision}`")]
        public static partial void GotContainerAppRevision(ILogger logger, string appName, string revision);
    }
}