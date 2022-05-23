// Application insights
resource "azurerm_log_analytics_workspace" "aca-test-ws" {
  name                = "aca-workspace-test"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_application_insights" "aca-test-ai" {
  name                = "aca-test-appinsights"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  workspace_id        = azurerm_log_analytics_workspace.aca-test-ws.id
  application_type    = "web"
  tags                = local.tags
}
