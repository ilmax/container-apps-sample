output "instrumentation_key" {
  value     = azurerm_application_insights.aca-ai.instrumentation_key
  sensitive = true
}

output "queue_name" {
  value = azurerm_servicebus_queue.aca-queue.name
}
