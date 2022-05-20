locals {
  tags = {
    managed-by  = "terraform"
    environment = var.environment
  }
}

// Resource group
resource "azurerm_resource_group" "aca-test-rg" {
  name     = "azure-container-app-test"
  location = var.location
  tags     = local.tags
}