// Service bus
resource "azurerm_servicebus_namespace" "aca-test-sb" {
  name                = "aca-test-servicebus-ns"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  sku                 = "Basic"
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "aca-test-queue" {
  name                = "aca_servicebus_queue"
  namespace_id        = azurerm_servicebus_namespace.aca-test-sb.id
  enable_partitioning = true
}