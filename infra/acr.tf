// Container registry
resource "azurerm_container_registry" "aca-registry" {
  name                = "acaimageregistry"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  sku                 = "Basic"
  admin_enabled       = true

  tags = local.tags
}

resource "random_integer" "name" {
  min = 1
  max = 1000
}

# Execute the acr task we just created to build the container image
# azurerm_container_registry_task does not support execute on create (yet)
# https://github.com/hashicorp/terraform-provider-azurerm/issues/15095
resource "azapi_resource" "build-producer-acr-task" {
  name      = "build-producer-task${random_integer.name.id}"
  location  = var.location
  parent_id = azurerm_container_registry.aca-registry.id
  type      = "Microsoft.ContainerRegistry/registries/taskRuns@2019-06-01-preview"
  body = jsonencode({
    properties = {
      runRequest = {
        type           = "DockerBuildRequest"
        sourceLocation = "https://github.com/ilmax/container-apps-sample.git#main"
        dockerFilePath = "Sample.Producer/Dockerfile"
        platform = {
          os = "Linux"
        }
        imageNames = ["${var.producer_image_name}:{{.Run.ID}}", "${var.producer_image_name}:latest"]
      }
    }
  })
  ignore_missing_property = true
}

resource "azapi_resource" "build-consumer-acr-task" {
  name      = "build-consumer-task${random_integer.name.id}"
  location  = var.location
  parent_id = azurerm_container_registry.aca-registry.id
  type      = "Microsoft.ContainerRegistry/registries/taskRuns@2019-06-01-preview"
  body = jsonencode({
    properties = {
      runRequest = {
        type           = "DockerBuildRequest"
        sourceLocation = "https://github.com/ilmax/container-apps-sample.git#main"
        dockerFilePath = "Sample.Consumer/Dockerfile"
        platform = {
          os = "Linux"
        }
        imageNames = ["${var.consumer_image_name}:{{.Run.ID}}", "${var.consumer_image_name}:latest"]
      }
    }
  })
  ignore_missing_property = true
}