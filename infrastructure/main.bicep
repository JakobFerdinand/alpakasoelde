targetScope = 'resourceGroup'

@description('Name of the storage account')
param storageAccountName string = 'alpakasoeldestorage'

@description('Name of the website static web app')
param websiteName string = 'alpakasoelde-website'

@description('Name of the dashboard static web app')
param dashboardName string = 'alpakasoelde-dashboard'

@description('Resource location')
param location string = resourceGroup().location

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

module website './modules/staticWebApp.bicep' = {
  name: 'website'
  params: {
    name: websiteName
    location: location
    customDomain: 'alpakasoelde.at'
    sku: 'Free'
  }
}

module dashboard './modules/staticWebApp.bicep' = {
  name: 'dashboard'
  params: {
    name: dashboardName
    location: location
    customDomain: 'dashboard.alpakasoelde.at'
    sku: 'Free'
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output websiteHost string = website.outputs.defaultHostname
output dashboardHost string = dashboard.outputs.defaultHostname
