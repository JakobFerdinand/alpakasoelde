@description('Name of the new Key Vault.')
param keyVaultName string

@description('Region of the Key Vault.')
param location string

@description('Azure AD tenant that owns the vault.')
param tenantId string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

output id string = keyVault.id
output name string = keyVault.name