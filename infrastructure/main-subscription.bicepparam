using './main-subscription.bicep'

param resourceGroupName = 'RG-Alpakasoelde'
param budgetName = 'Alpakasoelde-Budget'
param amount = 3
param startDate = '2025-06-01T00:00:00Z'
param endDate = '2030-05-31T00:00:00Z'
param actionGroupName = 'alpakasoelde-budget-actions'