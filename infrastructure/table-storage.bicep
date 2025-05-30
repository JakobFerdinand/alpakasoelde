targetScope = 'resourceGroup'

@description('Name of the storage account to create')
param storageAccountName string = 'alpakasoelde'

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2022-09-01' = {
  name: '${storageAccount.name}/default'
  dependsOn: [
    storageAccount
  ]
}

output storageAccountName string = storageAccount.name
