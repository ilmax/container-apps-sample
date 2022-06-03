using Azure.ResourceManager;
using Azure.ResourceManager.Applications.Containers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using Sample.HealthProbesInvoker.Config;

namespace Sample.HealthProbesInvoker
{
    public class RevisionSelector
    {
        private readonly ArmClient _client;
        private readonly ILogger<RevisionSelector> _logger;
        private readonly AzureConfig _azureConfig;

        public RevisionSelector(ArmClient client, IOptions<AzureConfig> azureConfig, ILogger<RevisionSelector> logger)
        {
            _client = client;
            _azureConfig = azureConfig.Value;
            _logger = logger;
        }

        public async Task<ContainerAppRevisionData> SelectRevisionAsync(string? resourceGroupName, string applicationName, string? revisionName)
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

            if (containerApp.Data.Configuration.Ingress is null)
            {
                _logger.LogError("Container app with name '{ca}' doesn't have an ingress, throwing exception", applicationName);
                throw new InvalidOperationException("Container app not doesn't have an ingress");
            }

            if (!string.IsNullOrEmpty(revisionName))
            {
                return await SelectRevisionByNameAsync(containerApp, revisionName);
            }

            return await SelectLatestRevisionAsync(containerApp);
        }

        private Task<ContainerAppRevisionData> SelectLatestRevisionAsync(ContainerAppResource containerApp)
        {
            return SelectRevisionByNameAsync(containerApp, containerApp.Data.LatestRevisionName);
        }

        private async Task<ContainerAppRevisionData> SelectRevisionByNameAsync(ContainerAppResource containerApp, string revisionName)
        {
            ContainerAppRevisionResource revision = await containerApp.GetContainerAppRevisions().GetAsync(revisionName);
            if (revision is null)
            {
                _logger.LogError("Container app with name '{ca}' doesn't have a revision named '{rn}'", containerApp.Data.Name, revisionName);
                throw new InvalidOperationException("Container app not doesn't have the requested revision");
            }

            _logger.LogInformation("Got a container app {app} revision", containerApp.Data.Name);
            return revision.Data;
        }
    }
}
