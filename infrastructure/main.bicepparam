using './main.bicep'

param storageAccountName = 'alpakasoelde'
param storageLocation = 'germanywestcentral'
param tables = [
  'alpakas'
  'events'
  'gutscheine'
  'messages'
]
param containers = [
  'alpakas'
  'event-documents'
]

param keyVaultName = 'kv-alpakasoelde'
param keyVaultLocation = 'germanywestcentral'

param communicationServiceName = 'acs-alpakasoelde'
param emailServiceName = 'alpakasoelde'
param communicationDataLocation = 'germany'
param emailDataLocation = 'Germany'
param managedDomainName = 'AzureManagedDomain'
param contactDomainName = 'kontakt.alpakasoelde.at'

param workspaceName = 'Alpakasoelde-LogAnalyticsWorkspace'
param insightsName = 'alpakasoelde-insights'
param observabilityLocation = 'germanywestcentral'
param workspaceRetentionInDays = 30
param budgetNotificationEmail = 'j.wegenschimmel@gmail.com'

param staticSitesLocation = 'westeurope'
param websiteSiteName = 'alpakasoelde'
param dashboardSiteName = 'alpakasoelde-dashboard'
param websiteCustomDomains = [
  'alpakasoelde.at'
  'www.alpakasoelde.at'
]
param dashboardCustomDomains = [
  'dashboard.alpakasoelde.at'
]

param openAiName = 'openai-alpakasoelde'
param openAiLocation = 'germanywestcentral'
param openAiDeploymentName = 'spam-classifier'
param openAiModelName = 'gpt-6-luna'
param openAiModelVersion = '2026-09-22'
param openAiCapacity = 20
param openAiAssistantDeploymentName = 'dashboard-assistant'
param openAiAssistantModelName = 'gpt-6-luna'
param openAiAssistantModelVersion = '2026-09-22'
param openAiAssistantCapacity = 10
