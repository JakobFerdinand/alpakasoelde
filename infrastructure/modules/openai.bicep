@description('Name of the Azure OpenAI account.')
param openAiName string

@description('Region of the Azure OpenAI account.')
param location string

@description('Model deployments to create on the account, each as { name, model, version, capacity }. Order is part of the contract: entry 0 is the spam classifier, entry 1 the dashboard assistant, and reordering would rename the deployed resources.')
@minLength(2)
param deployments array

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {}
}

// Serialised with @batchSize(1): Cognitive Services rejects concurrent
// deployment writes on the same account often enough to fail an apply.
@batchSize(1)
resource modelDeployments 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = [
  for deployment in deployments: {
    parent: openAi
    name: deployment.name
    properties: {
      model: {
        format: 'OpenAI'
        name: deployment.model
        version: deployment.version
      }
    }
    sku: {
      name: 'GlobalStandard'
      capacity: deployment.capacity
    }
  }
]

output endpoint string = openAi.properties.endpoint
output accountName string = openAi.name
output deploymentName string = modelDeployments[0].name
output assistantDeploymentName string = modelDeployments[1].name
