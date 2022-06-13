terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.9.0"
    }

    azapi = {
      source  = "Azure/azapi"
    }
  }
}

provider "azurerm" {
  features {}
}