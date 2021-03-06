// Container App
data "azurerm_subscription" "current" {}

// terraform doesn't support creating container apps yet https://github.com/hashicorp/terraform-provider-azurerm/issues/14122
resource "azapi_resource" "ace-external" {
  name      = "ace-external"
  type      = "Microsoft.App/managedEnvironments@2022-03-01"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  body = jsonencode({
    properties = {
      appLogsConfiguration = {
        destination = "log-analytics"
        logAnalyticsConfiguration = {
          customerId = azurerm_log_analytics_workspace.ace-ws.workspace_id
          sharedKey  = azurerm_log_analytics_workspace.ace-ws.primary_shared_key
        }
      }
    }
  })
  tags = local.tags
}

# resource "azapi_resource" "ace-internal" {
#   name      = "ace-internal"
#   type      = "Microsoft.App/managedEnvironments@2022-03-01"
#   location  = var.location
#   parent_id = azurerm_resource_group.aca-test-rg.id
#   body = jsonencode({
#     properties = {
#       appLogsConfiguration = {
#         destination = "log-analytics"
#         logAnalyticsConfiguration = {
#           customerId = azurerm_log_analytics_workspace.ace-ws.workspace_id
#           sharedKey  = azurerm_log_analytics_workspace.ace-ws.primary_shared_key
#         }
#       }
#       vnetConfiguration = {
#         internal               = false
#         infrastructureSubnetId = azurerm_subnet.ace-subnet.id
#       }
#     }
#   })
#   tags = local.tags
# }

resource "azapi_resource" "producer-container-app" {
  name      = "producer-containerapp"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    identity = {
      type = "SystemAssigned"
    }
    properties = {
      managedEnvironmentId = azapi_resource.ace-external.id
      configuration = {
        activeRevisionsMode = "multiple"
        ingress = {
          targetPort    = 80
          external      = true
          allowInsecure = false
        }
        registries = [
          {
            server            = azurerm_container_registry.aca-registry.login_server
            username          = azurerm_container_registry.aca-registry.admin_username
            passwordSecretRef = "registry-password"
          }
        ]
        secrets : [
          {
            name = "registry-password"
            # Todo: Container apps does not yet support Managed Identity connection to ACR
            value = azurerm_container_registry.aca-registry.admin_password
          }
        ]
      }
      template = {
        containers = [
          {
            image = "${azurerm_container_registry.aca-registry.login_server}/${var.producer_image_name}:latest"
            name  = "producer"
            resources = {
              cpu    = 0.25,
              memory = "0.5Gi"
            }
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY",
                "value" : azurerm_application_insights.aca-ai.instrumentation_key
              },
              {
                "name" : "ASPNETCORE_ENVIRONMENT",
                "value" : "Test"
              },
              {
                "name" : "ServiceBus__Namespace",
                "value" : "${azurerm_servicebus_namespace.aca-sb.name}.servicebus.windows.net"
              },
              {
                "name" : "ServiceBus__Queue",
                "value" : azurerm_servicebus_queue.aca-queue.name
              }
            ]
            probes = [
              {
                httpGet = {
                  path   = "/healthz",
                  port   = 80,
                  scheme = "HTTP"
                },
                failureThreshold    = 3
                successThreshold    = 1
                initialDelaySeconds = 10
                periodSeconds       = 10
                timeoutSeconds      = 5
                type                = "liveness"
              },
              {
                httpGet = {
                  path   = "/healthz",
                  port   = 80,
                  scheme = "HTTP"
                },
                failureThreshold    = 3
                successThreshold    = 1
                initialDelaySeconds = 10
                periodSeconds       = 10
                timeoutSeconds      = 5
                type                = "startup"
              },
              {
                tcpSocket = {
                  port = 80
                },
                failureThreshold    = 10
                initialDelaySeconds = 5
                periodSeconds       = 10
                timeoutSeconds      = 5
                type                = "readiness"
              }
            ]
          }
        ],
        # scale = {
        #   maxReplicas = 2
        #   minReplicas = 1
        #   rules = [
        #     {
        #       name = "http-scale-rule"
        #       http = {
        #         metadata = {
        #           concurrentRequests = "10"
        #         }
        #       }
        #     }
        #   ]
        # }
      }
    }
  })
  # This seems to be important for the private registry to work(?)
  ignore_missing_property = true
  # Depends on ACR building the image firest
  depends_on             = [azapi_resource.build-producer-acr-task]
  tags                   = local.tags
  response_export_values = ["properties.configuration.ingress"]
}

resource "azapi_resource" "consumer-container-app" {
  name      = "consumer-containerapp"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    identity = {
      type = "SystemAssigned"
    }
    properties = {
      managedEnvironmentId = azapi_resource.ace-external.id
      configuration = {
        registries = [
          {
            server            = azurerm_container_registry.aca-registry.login_server
            username          = azurerm_container_registry.aca-registry.admin_username
            passwordSecretRef = "registry-password"
          }
        ],
        secrets : [
          {
            name = "registry-password"
            # Todo: Container apps does not yet support Managed Identity connection to ACR
            value = azurerm_container_registry.aca-registry.admin_password
          },
          {
            name = "sb-conn-str"
            # TODO: Check if we can use KEDA scalers with Managed identity
            value = azurerm_servicebus_namespace.aca-sb.default_primary_connection_string
          }
        ]
      },
      template = {
        containers = [
          {
            image = "${azurerm_container_registry.aca-registry.login_server}/${var.consumer_image_name}:latest"
            name  = "consumer"
            resources = {
              cpu    = 0.25,
              memory = "0.5Gi"
            }
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY",
                "value" : azurerm_application_insights.aca-ai.instrumentation_key
              },
              {
                "name" : "ASPNETCORE_ENVIRONMENT",
                "value" : "Test"
              },
              {
                "name" : "ServiceBusConnection__fullyQualifiedNamespace",
                "value" : "${azurerm_servicebus_namespace.aca-sb.name}.servicebus.windows.net"
              },
              {
                "name" : "ServiceBusConnection__credential",
                "value" : "managedidentity"
              },
              {
                "name" : "QueueName",
                "value" : azurerm_servicebus_queue.aca-queue.name
              },
              {
                "name" : "ProducerBaseAddress",
                "value" : "https://${jsondecode(azapi_resource.producer-container-app.output).properties.configuration.ingress.fqdn}"
              },
              {
                "name" : "WEBSITE_CLOUD_ROLENAME",
                "value" : "Sample.Consumer"
              }
            ]
          }
        ]
        scale = {
          maxReplicas = 2
          minReplicas = 0
          rules = [
            {
              name = "sb-scale-rule"
              custom = {
                auth = [
                  {
                    secretRef        = "sb-conn-str"
                    triggerParameter = "connection"
                  }
                ]
                metadata = {
                  messageCount = "5"
                  queueName    = azurerm_servicebus_queue.aca-queue.name
                }
                type = "azure-servicebus"
              }
            }
          ]
        }
      }
    }
  })
  # This seems to be important for the private registry to work(?)
  ignore_missing_property = true
  # Depends on ACR building the image firest
  depends_on = [azapi_resource.build-producer-acr-task]
  tags       = local.tags
}

# resource "azapi_resource" "healthprobeinvoker-container-app" {
#   name      = "healthprobeinvoker-containerapp"
#   location  = var.location
#   parent_id = azurerm_resource_group.aca-test-rg.id
#   type      = "Microsoft.App/containerApps@2022-03-01"
#   body = jsonencode({
#     identity = {
#       type = "SystemAssigned"
#     }
#     properties = {
#       managedEnvironmentId = azapi_resource.ace-external.id
#       configuration = {
#         activeRevisionsMode = "single"
#         ingress = {
#           targetPort    = 80
#           external      = true
#           allowInsecure = false
#         }
#         registries = [
#           {
#             server            = azurerm_container_registry.aca-registry.login_server
#             username          = azurerm_container_registry.aca-registry.admin_username
#             passwordSecretRef = "registry-password"
#           }
#         ],
#         secrets : [
#           {
#             name = "registry-password"
#             # Todo: Container apps does not yet support Managed Identity connection to ACR
#             value = azurerm_container_registry.aca-registry.admin_password
#           }
#         ]
#       },
#       template = {
#         containers = [
#           {
#             image = "${azurerm_container_registry.aca-registry.login_server}/${var.healthprobeinvoker_image_name}:latest"
#             name  = "healthprobeinvoker"
#             resources = {
#               cpu    = 0.25,
#               memory = "0.5Gi"
#             }
#             env : [
#               {
#                 "name" : "APPINSIGHTS_INSTRUMENTATIONKEY",
#                 "value" : azurerm_application_insights.aca-ai.instrumentation_key
#               },
#               {
#                 "name" : "ASPNETCORE_ENVIRONMENT",
#                 "value" : "Test"
#               },
#               {
#                 "name" : "Azure__SubscriptionId",
#                 "value" : data.azurerm_subscription.current.subscription_id
#               },
#               {
#                 "name" : "Azure__ResourceGroupName",
#                 "value" : azurerm_resource_group.aca-test-rg.name
#               }
#             ]
#           }
#         ]
#         scale = {
#           maxReplicas = 1
#           minReplicas = 1
#         }
#       }
#     }
#   })
#   # This seems to be important for the private registry to work(?)
#   ignore_missing_property = true
#   # Depends on ACR building the image firest
#   depends_on = [azapi_resource.build-healthprobeinvoker-acr-task]
#   tags       = local.tags
# }

resource "azurerm_role_assignment" "producer-service-bus-write" {
  scope                = azurerm_servicebus_queue.aca-queue.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azapi_resource.producer-container-app.identity.0.principal_id
  depends_on           = [azapi_resource.producer-container-app]
}

resource "azurerm_role_assignment" "consumer-service-bus-read" {
  scope                = azurerm_servicebus_namespace.aca-sb.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azapi_resource.consumer-container-app.identity.0.principal_id
  depends_on           = [azapi_resource.consumer-container-app]
}
