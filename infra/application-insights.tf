// Application insights
resource "azurerm_log_analytics_workspace" "ace-ws" {
  name                = "aca-workspace"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_application_insights" "aca-ai" {
  name                = "aca-appinsights"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  workspace_id        = azurerm_log_analytics_workspace.ace-ws.id
  application_type    = "web"
  tags                = local.tags
}
