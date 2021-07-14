output "sb_primary" {
  sensitive = true
  value = (
    length(azurerm_servicebus_namespace.sb_namespace[*]) > 0
    ? azurerm_servicebus_namespace.sb_namespace[0].default_primary_connection_string
    : "n/a"
  )
}

output "sb_secondary" {
  sensitive = true
  value = (
    length(azurerm_servicebus_namespace.sb_namespace[*]) > 0
    ? azurerm_servicebus_namespace.sb_namespace[0].default_secondary_connection_string
    : "n/a"
  )
}

output "signalr_primary" {
  sensitive = true
  value = (
    length(azurerm_signalr_service.signalr[*]) > 0
    ? azurerm_signalr_service.signalr[0].primary_connection_string
    : "n/a"
  )
}
