param storageAccountName string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
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
}

resource messagesTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  name: '${tableService.name}/messages'
}

resource alpakasTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-09-01' = {
  name: '${tableService.name}/alpakas'
}

output storageAccountName string = storageAccount.name
