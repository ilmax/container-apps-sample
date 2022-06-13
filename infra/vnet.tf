resource "azurerm_virtual_network" "aca-test-vnet" {
  name                = "aca-test-vnet"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  address_space       = ["10.0.0.0/16"]

  tags = local.tags
}

resource "azurerm_subnet" "ace-subnet" {
  name                 = "ace-subnet"
  resource_group_name  = azurerm_resource_group.aca-test-rg.name
  virtual_network_name = azurerm_virtual_network.aca-test-vnet.name
  address_prefixes     = ["10.0.0.0/21"]
}
