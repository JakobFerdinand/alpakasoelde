param name string
param location string
param sku string = 'Free'
@description('Optional custom domain to assign to the static web app')
param customDomain string = ''

resource staticWebApp 'Microsoft.Web/staticSites@2022-03-01' = {
  name: name
  location: location
  sku: {
    name: sku
    tier: sku
  }
  properties: {}
}

resource customDomainResource 'Microsoft.Web/staticSites/customDomains@2022-09-01' = if (customDomain != '') {
  name: '${staticWebApp.name}/${customDomain}'
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
