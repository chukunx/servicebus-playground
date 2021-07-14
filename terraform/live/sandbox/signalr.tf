resource "azurerm_signalr_service" "signalr" {
  count               = (var.switch_signalr ? 1 : 0)
  name                = "signalr-${var.resource_type}-${var.env}-${var.region_short}-01"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  sku {
    name     = "Free_F1" # Standard_S1
    capacity = 1
  }

  cors {
    allowed_origins = [
      "http://localhost:4500"
    ]
  }

  features {
    flag  = "ServiceMode"
    value = "Default"
  }

  tags = merge(local.shared_tags, {})
}
