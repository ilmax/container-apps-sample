resource "azurerm_virtual_network" "aca-vnet" {
  name                = "aca-vnet"
  location            = var.location
  resource_group_name = azurerm_resource_group.aca-test-rg.name
  address_space       = ["10.0.0.0/16"]

  tags = local.tags
}

resource "azurerm_subnet" "aca-subnet" {
  name                 = "aca-subnet"
  resource_group_name  = azurerm_resource_group.aca-test-rg.name
  virtual_network_name = azurerm_virtual_network.aca-vnet.name
  address_prefixes     = ["10.0.16.0/23"]
}