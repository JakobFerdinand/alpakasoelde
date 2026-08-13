@description('Name of the public static web app to adopt.')
param websiteSiteName string

@description('Name of the internal dashboard static web app to adopt.')
param dashboardSiteName string

@description('Region of the static web apps.')
param location string

@description('Custom domains for the public site.')
param websiteCustomDomains array = []

@description('Custom domains for the dashboard.')
param dashboardCustomDomains array = []

resource website 'Microsoft.Web/staticSites@2023-12-01' = {
  name: websiteSiteName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource dashboard 'Microsoft.Web/staticSites@2023-12-01' = {
  name: dashboardSiteName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

resource websiteCustomDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = [for domain in websiteCustomDomains: {
  parent: website
  name: domain
}]

resource dashboardCustomDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = [for domain in dashboardCustomDomains: {
  parent: dashboard
  name: domain
}]

output websiteId string = website.id
output dashboardId string = dashboard.id