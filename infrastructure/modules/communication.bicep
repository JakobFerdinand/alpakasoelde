@description('Name of the existing Communication Services resource to adopt.')
param communicationServiceName string

@description('Name of the existing Email Services resource to adopt.')
param emailServiceName string

@description('Data location (lowercase) of the Communication Services resource.')
param communicationDataLocation string

@description('Data location of the Email Services resource.')
param emailDataLocation string

@description('Name of the Azure-managed email domain.')
param managedDomainName string = 'AzureManagedDomain'

@description('Name of the customer-managed email domain.')
param contactDomainName string

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  properties: {
    dataLocation: emailDataLocation
  }
}

resource managedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: managedDomainName
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

resource contactDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: contactDomainName
  location: 'global'
  properties: {
    domainManagement: 'CustomerManaged'
  }
}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationServiceName
  location: 'global'
  properties: {
    dataLocation: communicationDataLocation
    linkedDomains: [
      managedDomain.id
      contactDomain.id
    ]
  }
}

output communicationServiceId string = communicationService.id
output emailServiceId string = emailService.id
output contactDomainId string = contactDomain.id