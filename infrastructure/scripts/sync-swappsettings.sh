#!/usr/bin/env bash
set -euo pipefail

# Re-applies the Static Web App app settings from Key Vault. Called by the
# infra-deploy workflow after every infrastructure deployment so that app
# settings always reflect the secrets stored in the vault
# (`az staticwebapp appsettings set` replaces the full settings list).
#
# Requirements:
#   - az (logged in with access to the vault and the static web apps)
#
# Secrets are never printed; only the applied key names are shown.

RESOURCE_GROUP="${RESOURCE_GROUP:-RG-Alpakasoelde}"
KEY_VAULT="${KEY_VAULT:-kv-alpakasoelde}"
WEBSITE_SITE="${WEBSITE_SITE:-alpakasoelde}"
DASHBOARD_SITE="${DASHBOARD_SITE:-alpakasoelde-dashboard}"

WEBSITE_KEYS=(StorageConnection AZURE_STORAGE_ACCOUNT_KEY AZURE_STORAGE_ACCOUNT_NAME APPLICATIONINSIGHTS_CONNECTION_STRING EmailConnection EmailSenderAddress ReceiverEmailAddresses)
DASHBOARD_KEYS=(StorageConnection AZURE_STORAGE_ACCOUNT_KEY AZURE_STORAGE_ACCOUNT_NAME)

# Key Vault secret names only allow alphanumerics and hyphens, so app setting
# names are stored with '_' replaced by '-' (see seed-keyvault.sh).
normalize_name() {
  printf '%s' "$1" | tr '_' '-'
}

fetch_secret() {
  az keyvault secret show \
    --vault-name "$KEY_VAULT" \
    --name "$(normalize_name "$1")" \
    --query value \
    -o tsv
}

apply_to_site() {
  local site="$1"
  local -a keys=("${@:2}")
  local -a args=()
  local key

  for key in "${keys[@]}"; do
    args+=("$key=$(fetch_secret "$key")")
  done

  echo "Applying app settings to $site: ${keys[*]}"
  az staticwebapp appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$site" \
    --settings "${args[@]}" \
    --only-show-errors >/dev/null
}

apply_to_site "$WEBSITE_SITE" "${WEBSITE_KEYS[@]}"
apply_to_site "$DASHBOARD_SITE" "${DASHBOARD_KEYS[@]}"

echo "Static Web App app settings synced from Key Vault."