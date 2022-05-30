# container-apps-sample
A sample repository to poke around with Azure Container Apps, Azure Service Bus, terraform and end to end tracing with Application Insights

# Deploy the application:
1. Clone the repo
2. Install terraform
3. cd infra
4. run `terraform init`
5. run `terraform apply` and answer with yes 

# Debug locally
After creating the required infrastructure, you need to either replace few configuration values.

## Producer
Fill in the following values in the **appsettings.json** (or better using user secrets)
```json
"ServiceBus": {
  "Namespace": "set by terraform",
  "Queue": "set by terraform"
}
```

## Consumer
Fill in the following values in the **appsettings.json** (or better using user secrets)
```json	
"ServiceBusConnection": {
  "fullyQualifiedNamespace": "set by terraform"
},
"QueueName": "set by terraform"
```	