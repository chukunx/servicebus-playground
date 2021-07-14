terraform {
  required_version = ">= 0.12"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 2.49.0"
    }
  }
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.resource_type}-${var.env}-${var.region_short}-01"
  location = var.region
}
