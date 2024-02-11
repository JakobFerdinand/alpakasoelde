
@description('Alpakasöde Homepage')
resource alpakasoelde 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'alpakasoelde'
  location: 'Central US'
  tags: {}
  properties: {
    repositoryUrl: 'https://github.com/JakobFerdinand/alpakasoelde'
    branch: 'main'
    stableInboundIP: '20.221.45.47'
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    provider: 'GitHub'
    enterpriseGradeCdnStatus: 'Disabled'
    trafficSplitting: {
      environmentDistribution: {
        default: 100
      }
    }
    areStaticSitesDistributedBackendsEnabled: false
  }
  sku: {
    name: 'Free'
    tier: 'Free'
  }
}
