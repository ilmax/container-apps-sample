locals {
  serverAppName = "producer-containerapp-internal"
}

// Container App creation
// terraform doesn't support creating container apps yet https://github.com/hashicorp/terraform-provider-azurerm/issues/14122
resource "azapi_resource" "ace-internal" {
  name      = "ace-internal"
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
      vnetConfiguration = {
        internal               = false
        infrastructureSubnetId = resource.azurerm_subnet.aca-subnet.id
      }
    }
  })
  tags = local.tags
}

resource "random_uuid" "producer-role-id" {}

resource "azuread_application" "producer-container-app-application" {
  display_name = local.serverAppName

  identifier_uris = ["api://${local.serverAppName}"]

  api {
    requested_access_token_version = 2
  }

  app_role {
    allowed_member_types = ["User", "Application"]
    description          = "Call Api"
    display_name         = "Call Api"
    enabled              = true
    id                   = random_uuid.producer-role-id.result
    value                = "user_impersonation"
  }
}

resource "azuread_service_principal" "producer-container-app-internal-sp" {
  application_id = azuread_application.producer-container-app-application.application_id
  # owners         = [data.azuread_client_config.current.object_id]
}

resource "azapi_resource" "producer-container-app-internal" {
  name      = "producer-containerapp-internal"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    identity = {
      type = "SystemAssigned"
    }
    properties = {
      managedEnvironmentId = azapi_resource.ace-internal.id
      configuration = {
        activeRevisionsMode = "single"
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
              cpu    = 0.25
              memory = "0.5Gi"
            }
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY"
                "value" : azurerm_application_insights.aca-ai.instrumentation_key
              },
              {
                "name" : "ASPNETCORE_ENVIRONMENT"
                "value" : "Test"
              },
              {
                "name" : "ServiceBus__Namespace"
                "value" : "${azurerm_servicebus_namespace.aca-sb.name}.servicebus.windows.net"
              },
              {
                "name" : "ServiceBus__Queue"
                "value" : azurerm_servicebus_queue.aca-queue.name
              },
              {
                "name" : "AzureAd__Instance"
                "value" : "https://login.microsoftonline.com/"
              },
              {
                "name" : "AzureAd__TenantId"
                "value" : data.azurerm_subscription.current.tenant_id
              },
              {
                "name" : "Logging__LogLevel__Default"
                "value" : "Debug"
              },
              {
                "name" : "AzureAd__ClientId"
                "value" : azuread_application.producer-container-app-application.application_id
              },
              {
                "name" : "AzureAd__TokenValidationParameters__RoleClaimType"
                "value" : "roles"
              },
              {
                "name" : "AzureAd__TokenValidationParameters__ValidAudience"
                "value" : azuread_application.producer-container-app-application.application_id
              },
              {
                "name" : "AzureAd__TokenValidationParameters__ValidIssuer"
                "value" : "https://login.microsoftonline.com/${data.azurerm_subscription.current.tenant_id}/v2.0"
              }
            ]
            probes = [
              {
                httpGet = {
                  path   = "/healthz/liveness"
                  port   = 80
                  scheme = "HTTP"
                }
                failureThreshold    = 3
                successThreshold    = 1
                initialDelaySeconds = 10
                periodSeconds       = 10
                timeoutSeconds      = 10
                type                = "liveness"
              },
              {
                httpGet = {
                  path   = "/healthz/startup"
                  port   = 80
                  scheme = "HTTP"
                }
                failureThreshold    = 3
                successThreshold    = 1
                initialDelaySeconds = 10
                periodSeconds       = 10
                timeoutSeconds      = 10
                type                = "startup"
              },
              {
                tcpSocket = {
                  port = 80
                }
                failureThreshold    = 10
                initialDelaySeconds = 10
                periodSeconds       = 10
                timeoutSeconds      = 10
                type                = "readiness"
              }
            ]
          }
        ]
        scale = {
          maxReplicas = 1
          minReplicas = 1
        }
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

resource "azapi_resource" "consumer-container-app-internal" {
  name      = "consumer-containerapp-internal"
  location  = var.location
  parent_id = azurerm_resource_group.aca-test-rg.id
  type      = "Microsoft.App/containerApps@2022-03-01"
  body = jsonencode({
    identity = {
      type = "SystemAssigned"
    }
    properties = {
      managedEnvironmentId = azapi_resource.ace-internal.id
      configuration = {
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
          },
          {
            name = "sb-conn-str"
            # TODO: Check if we can use KEDA scalers with Managed identity
            value = azurerm_servicebus_namespace.aca-sb.default_primary_connection_string
          }
        ]
      }
      template = {
        containers = [
          {
            image = "${azurerm_container_registry.aca-registry.login_server}/${var.consumer_image_name}:latest"
            name  = "consumer"
            resources = {
              cpu    = 0.25
              memory = "0.5Gi"
            }
            env : [
              {
                "name" : "APPINSIGHTS_INSTRUMENTATIONKEY"
                "value" : azurerm_application_insights.aca-ai.instrumentation_key
              },
              {
                "name" : "ASPNETCORE_ENVIRONMENT"
                "value" : "Test"
              },
              {
                "name" : "ServiceBusConnection__fullyQualifiedNamespace"
                "value" : "${azurerm_servicebus_namespace.aca-sb.name}.servicebus.windows.net"
              },
              {
                "name" : "ServiceBusConnection__credential"
                "value" : "managedidentity"
              },
              {
                "name" : "QueueName"
                "value" : azurerm_servicebus_queue.aca-queue.name
              },
              {
                "name" : "ProducerBaseAddress"
                "value" : "https://${jsondecode(azapi_resource.producer-container-app-internal.output).properties.configuration.ingress.fqdn}"
              },
              {
                "name" : "WEBSITE_CLOUD_ROLENAME"
                "value" : "Sample.Consumer"
              },
              {
                "name" : "TenantId"
                "value" : data.azurerm_subscription.current.tenant_id
              },
              {
                "name" : "Server__ApplicationId"
                "value" : azuread_application.producer-container-app-application.application_id
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

resource "azurerm_role_assignment" "producer-internal-service-bus-write" {
  scope                = azurerm_servicebus_queue.aca-queue.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azapi_resource.producer-container-app-internal.identity.0.principal_id
  depends_on           = [azapi_resource.producer-container-app-internal]
}

resource "azurerm_role_assignment" "consumer-internal-service-bus-read" {
  scope                = azurerm_servicebus_namespace.aca-sb.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azapi_resource.consumer-container-app-internal.identity.0.principal_id
  depends_on           = [azapi_resource.consumer-container-app-internal]

  provisioner "local-exec" {
    command     = "New-AzureADServiceAppRoleAssignment -Id ${random_uuid.producer-role-id.result} -ObjectId ${azapi_resource.consumer-container-app-internal.identity.0.principal_id} -PrincipalId ${azapi_resource.consumer-container-app-internal.identity.0.principal_id} -ResourceId ${azuread_application.producer-container-app-application.object_id}"
    interpreter = ["PowerShell", "-Command"]
  }
}

resource "null_resource" "permissions" {
  depends_on = [
    azapi_resource.consumer-container-app-internal,
    azapi_resource.producer-container-app-internal,
    azuread_service_principal.producer-container-app-internal-sp
  ]
  provisioner "local-exec" {
    command = <<EOT
    # The managed identity that will be assigned the permissions (i.e. the client app)
    $miObjectID = "${azapi_resource.consumer-container-app-internal.identity.0.principal_id}"

    # The app registration ID of the API that's exposing the permissions (i.e. the server app)
    $appId = "${azuread_application.producer-container-app-application.application_id}"

    # The service principal ID of the API that's exposing the permissions (i.e. the server app)
    $app = "${azuread_service_principal.producer-container-app-internal-sp.object_id}"

    # The Id of the role that the client app will be assigned
    $role = "${random_uuid.producer-role-id.result}"

    ## Do not require user interaction to call the Connect-AAzureAD cmdlet
    $adtoken_container = (az account get-access-token --resource "https://graph.windows.net/" | ConvertFrom-Json)
    $account = (az account show | ConvertFrom-Json)
    $adtoken = $adtoken_container.accessToken
    $user_name = $account.user.name
    $tenant = $account.tenantId
    
    # Connect to Azure AD
    Connect-AzureAD -aadaccesstoken "$adtoken" -accountid "$user_name" -tenantid "$tenant"

    # Finally grant the permissions if they're not there already
    $permission = Get-AzureADServiceAppRoleAssignment -ObjectId $miObjectID
    if ($permission -eq 0) {
      New-AzureADServiceAppRoleAssignment -Id $role -ObjectId $miObjectID -PrincipalId $miObjectID -ResourceId $app
    }
    EOT

    interpreter = ["PowerShell", "-Command"]
  }
}
