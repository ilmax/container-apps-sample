terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.15.1"
    }

    azapi = {
      source = "Azure/azapi"
    }

    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.15.0"
    }

    random = {
      source  = "hashicorp/random"
      version = "3.4.3"
    }
  }
}

provider "azuread" {
  tenant_id = "ff9c3fd8-7d5d-4427-9d12-6b0bf0038023"
}

provider "azurerm" {
  features {}
}
