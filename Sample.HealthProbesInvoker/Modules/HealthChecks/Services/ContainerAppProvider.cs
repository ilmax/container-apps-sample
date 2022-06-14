using Azure.ResourceManager;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using Sample.HealthProbesInvoker.Config;

namespace Sample.HealthProbesInvoker.Modules.HealthChecks.Services;

public class ContainerAppProvider
{
    private readonly ArmClient _client;
    private readonly ILogger<ContainerAppProvider> _logger;
    private readonly AzureConfig _azureConfig;

    public ContainerAppProvider(ArmClient client, IOptions<AzureConfig> azureConfig, ILogger<ContainerAppProvider> logger)
    {
        _client = client;
        _azureConfig = azureConfig.Value;
        _logger = logger;
    }

    public async Task<ContainerAppResource> GetContainerAppAsync(string? resourceGroupName, string applicationName)
    {
        _logger.LogInformation("Getting a container app {app} revision", applicationName);
        SubscriptionResource subscription = await _client.GetSubscriptions().GetAsync(_azureConfig.SubscriptionId);

        if (subscription is null)
        {
            _logger.LogError("Unable to find a subscription, throwing exception");
            throw new InvalidOperationException("Subscription not found");
        }

        ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName ?? _azureConfig.ResourceGroupName);
        if (resourceGroup is null)
        {
            _logger.LogError("Unable to find a resource group with name '{rg}', throwing exception", resourceGroupName);
            throw new InvalidOperationException("Resource group not found");
        }

        ContainerAppResource containerApp = await resourceGroup.GetContainerAppAsync(applicationName);
        if (containerApp is null)
        {
            _logger.LogError("Unable to find a container app with name '{ca}', throwing exception", applicationName);
            throw new InvalidOperationException("Container app not found");
        }

        return containerApp;
    }

    public async Task<ContainerAppRevisionData> GetRevisionAsync(string? resourceGroupName, string applicationName, string? revisionName)
    {
        ContainerAppResource containerApp = await GetContainerAppAsync(resourceGroupName, applicationName);
        if (containerApp is null)
        {
            _logger.LogError("Unable to find a container app with name '{ca}', throwing exception", applicationName);
            throw new InvalidOperationException("Container app not found");
        }

        return await GetRevisionAsync(containerApp, revisionName);
    }

    public async Task<ContainerAppRevisionData> GetRevisionAsync(ContainerAppResource containerApp, string? revisionName)
    {
        if (containerApp == null) throw new ArgumentNullException(nameof(containerApp));

        if (containerApp.Data.Configuration.Ingress is null)
        {
            _logger.LogError("Container app with name '{ca}' doesn't have an ingress, throwing exception", containerApp.Data.Name);
            throw new InvalidOperationException("Container app not doesn't have an ingress");
        }

        var containerAppRevisionResource = !string.IsNullOrEmpty(revisionName) 
            ? await GetRevisionResourceByNameAsync(containerApp, revisionName) 
            : await GetLatestRevisionAsync(containerApp);

        return containerAppRevisionResource.Data;
    }

    public async Task<ContainerAppRevisionResource> GetRevisionResourceByNameAsync(ContainerAppResource containerApp, string revisionName)
    {
        ContainerAppRevisionResource revision = await containerApp.GetContainerAppRevisions().GetAsync(revisionName);
        if (revision is null)
        {
            _logger.LogError("Container app with name '{ca}' doesn't have a revision named '{rn}'", containerApp.Data.Name, revisionName);
            throw new InvalidOperationException("Container app not doesn't have the requested revision");
        }

        _logger.LogInformation("Got a container app {app} revision", containerApp.Data.Name);
        return revision;
    }

    private Task<ContainerAppRevisionResource> GetLatestRevisionAsync(ContainerAppResource containerApp)
    {
        return GetRevisionResourceByNameAsync(containerApp, containerApp.Data.LatestRevisionName);
    }
}