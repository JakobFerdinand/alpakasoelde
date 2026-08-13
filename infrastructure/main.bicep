targetScope = 'resourceGroup'

@description('Name of the existing storage account.')
param storageAccountName string

@description('Region of the storage account.')
param storageLocation string

@description('Table names to manage on the storage account.')
param tables array = []

@description('Blob container names to manage on the storage account.')
param containers array = []

@description('Name of the new Key Vault.')
param keyVaultName string

@description('Region of the Key Vault.')
param keyVaultLocation string

@description('Name of the existing Communication Services resource.')
param communicationServiceName string

@description('Name of the existing Email Services resource.')
param emailServiceName string

@description('Data location (lowercase) of the Communication Services resource.')
param communicationDataLocation string

@description('Data location of the Email Services resource.')
param emailDataLocation string

@description('Name of the Azure-managed email domain.')
param managedDomainName string = 'AzureManagedDomain'

@description('Name of the customer-managed email domain.')
param contactDomainName string

@description('Name of the Log Analytics workspace.')
param workspaceName string

@description('Name of the Application Insights component.')
param insightsName string

@description('Region of the observability resources.')
param observabilityLocation string

@description('Retention (days) of the Log Analytics workspace.')
param workspaceRetentionInDays int = 30

@description('Email address used for budget notifications (non-secret configuration).')
param budgetNotificationEmail string

@description('Region of the static web apps.')
param staticSitesLocation string

@description('Name of the public static web app.')
param websiteSiteName string

@description('Name of the internal dashboard static web app.')
param dashboardSiteName string

@description('Custom domains for the public site.')
param websiteCustomDomains array = []

@description('Custom domains for the dashboard.')
param dashboardCustomDomains array = []

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageAccountName: storageAccountName
    location: storageLocation
    tables: tables
    containers: containers
  }
}

module keyvault './modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    keyVaultName: keyVaultName
    location: keyVaultLocation
    tenantId: tenant().tenantId
  }
}

module communication './modules/communication.bicep' = {
  name: 'communication'
  params: {
    communicationServiceName: communicationServiceName
    emailServiceName: emailServiceName
    communicationDataLocation: communicationDataLocation
    emailDataLocation: emailDataLocation
    managedDomainName: managedDomainName
    contactDomainName: contactDomainName
  }
}

module observability './modules/observability.bicep' = {
  name: 'observability'
  params: {
    workspaceName: workspaceName
    insightsName: insightsName
    location: observabilityLocation
    workspaceRetentionInDays: workspaceRetentionInDays
    insightsRetentionInDays: 90
    budgetNotificationEmail: budgetNotificationEmail
  }
}

module staticSites './modules/static-sites.bicep' = {
  name: 'staticSites'
  params: {
    websiteSiteName: websiteSiteName
    dashboardSiteName: dashboardSiteName
    location: staticSitesLocation
    websiteCustomDomains: websiteCustomDomains
    dashboardCustomDomains: dashboardCustomDomains
  }
}