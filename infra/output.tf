output "instrumentation_key" {
  value     = azurerm_application_insights.aca-test-ai.instrumentation_key
  sensitive = true
}

output "queue_name" {
  value = azurerm_servicebus_queue.aca-test-queue.name
}
