// Container App
// terraform doesn't support creating container apps yet https://github.com/hashicorp/terraform-provider-azurerm/issues/14122
resource "azapi_resource" "aca-test-environment" {
  name      = "aca-test-environment"
  type      = "Microsoft.App/managedEnvironments@2022-03-01"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  body = jsonencode({
    properties = {
      appLogsConfiguration = {
        destination = "log-analytics"
        logAnalyticsConfiguration = {
          customerId = azurerm_log_analytics_workspace.aca-test-ws.workspace_id
          sharedKey  = azurerm_log_analytics_workspace.aca-test-ws.primary_shared_key
        }
      }
    }
  })
  tags = local.tags
}

resource "azapi_resource" "producer_container_app" {
  name      = "producer-containerapp"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    properties = {
      managedEnvironmentId = azapi_resource.aca-test-environment.id
      configuration = {
        ingress = {
          targetPort = 80
          external   = true
        },
        registries = [
          {
            server            = azurerm_container_registry.aca-test-registry.login_server
            username          = azurerm_container_registry.aca-test-registry.admin_username
            passwordSecretRef = "registry-password"
          }
        ],
        secrets : [
          {
            name = "registry-password"
            # Todo: Container apps does not yet support Managed Identity connection to ACR
            value = azurerm_container_registry.aca-test-registry.admin_password
          }
        ]
      },
      template = {
        containers = [
          {
            image = "${azurerm_container_registry.aca-test-registry.login_server}/${var.producer_image_name}:latest"
            name  = "producer",
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY",
                "value" : azurerm_application_insights.aca-test-ai.instrumentation_key
              },
              {
                "name" : "ServiceBus__Namespace",
                "value" : azurerm_servicebus_namespace.aca-test-sb.name
              },
              {
                "name" : "ServiceBus__Queue",
                "value" : azurerm_servicebus_queue.aca-test-queue.name
              }
            ]
          }
        ]
      }
    }
  })
  # This seems to be important for the private registry to work(?)
  ignore_missing_property = true
  # Depends on ACR building the image firest
  depends_on = [azapi_resource.build_producer_acr_task]
}

resource "azapi_resource" "consumer_container_app" {
  name      = "consumer-containerapp"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    properties = {
      managedEnvironmentId = azapi_resource.aca-test-environment.id
      configuration = {
        registries = [
          {
            server            = azurerm_container_registry.aca-test-registry.login_server
            username          = azurerm_container_registry.aca-test-registry.admin_username
            passwordSecretRef = "registry-password"
          }
        ],
        secrets : [
          {
            name = "registry-password"
            # Todo: Container apps does not yet support Managed Identity connection to ACR
            value = azurerm_container_registry.aca-test-registry.admin_password
          }
        ]
      },
      template = {
        containers = [
          {
            image = "${azurerm_container_registry.aca-test-registry.login_server}/${var.consumer_image_name}:latest"
            name  = "consumer"
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY",
                "value" : azurerm_application_insights.aca-test-ai.instrumentation_key
              },
              {
                "name" : "ServiceBusConnection__fullyQualifiedNamespace",
                "value" : "${azurerm_servicebus_namespace.aca-test-sb.name}.servicebus.windows.net"
              },
              {
                "name" : "QueueName",
                "value" : azurerm_servicebus_queue.aca-test-queue.name
              }
            ]
          }
        ]
      }
    }
  })
  # This seems to be important for the private registry to work(?)
  ignore_missing_property = true
  # Depends on ACR building the image firest
  depends_on = [azapi_resource.build_producer_acr_task]
}
