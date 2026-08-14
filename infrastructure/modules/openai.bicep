@description('Name of the Azure OpenAI account.')
param openAiName string

@description('Region of the Azure OpenAI account.')
param location string

@description('Name of the model deployment used by the spam classifier.')
param deploymentName string

@description('Model to deploy for the spam classifier.')
param modelName string

@description('Model version to deploy for the spam classifier.')
param modelVersion string

@description('Global Standard deployment capacity in thousands of tokens per minute.')
param capacity int = 20

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {}
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: deploymentName
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
  sku: {
    name: 'GlobalStandard'
    capacity: capacity
  }
}

output endpoint string = openAi.properties.endpoint
output accountName string = openAi.name
output deploymentName string = modelDeployment.name