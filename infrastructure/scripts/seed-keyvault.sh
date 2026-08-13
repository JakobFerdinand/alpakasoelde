#!/usr/bin/env bash
set -euo pipefail

# Seeds kv-alpakasoelde with the app settings currently configured on the two
# Static Web Apps. Intended to run once manually before the first infrastructure
# deployment, but safe to re-run: existing secrets are overwritten with the
# current values.
#
# Requirements:
#   - az (logged in), jq
#   - Role assignment capability (Owner) so it can grant itself Key Vault
#     Administrator if the vault uses RBAC and the caller has no access yet.
#
# Secrets are never printed to the console; only the stored key names are shown.

RESOURCE_GROUP="${RESOURCE_GROUP:-RG-Alpakasoelde}"
KEY_VAULT="${KEY_VAULT:-kv-alpakasoelde}"
KEY_VAULT_LOCATION="${KEY_VAULT_LOCATION:-germanywestcentral}"
SITES=(alpakasoelde alpakasoelde-dashboard)

keyvault_exists() {
  az keyvault show --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" >/dev/null 2>&1
}

ensure_keyvault_admin() {
  local principal
  principal="$(az ad signed-in-user show --query id -o tsv)"
  local role
  role="$(
    az role assignment list \
      --scope "$KEY_VAULT_ID" \
      --assignee "$principal" \
      --query "[?roleDefinitionName=='Key Vault Administrator' || roleDefinitionName=='Owner'].roleDefinitionName | [0]" \
      -o tsv
  )"
  if [[ -z "$role" ]]; then
    echo "Granting 'Key Vault Administrator' to the current user on $KEY_VAULT..."
    az role assignment create \
      --role "Key Vault Administrator" \
      --assignee-object-id "$principal" \
      --assignee-principal-type User \
      --scope "$KEY_VAULT_ID" \
      --only-show-errors >/dev/null
  fi
}

seed_site() {
  local site="$1"
  local tmp_json
  tmp_json="$(mktemp)"
  local key value

  az staticwebapp appsettings list \
    --resource-group "$RESOURCE_GROUP" \
    --name "$site" \
    --query properties \
    -o json >"$tmp_json"

  while read -r key; do
    [[ -z "$key" ]] && continue
    value="$(jq -r --arg k "$key" '.[$k]' "$tmp_json")"
    az keyvault secret set \
      --vault-name "$KEY_VAULT" \
      --name "$key" \
      --value "$value" \
      --only-show-errors >/dev/null
    echo "  seeded: $key (from $site)"
  done < <(jq -r 'keys[]' "$tmp_json")

  rm -f "$tmp_json"
}

if ! keyvault_exists; then
  echo "Creating Key Vault $KEY_VAULT..."
  az keyvault create \
    --name "$KEY_VAULT" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$KEY_VAULT_LOCATION" \
    --sku standard \
    --enable-rbac-authorization \
    --enable-soft-delete \
    --retention-days 90 \
    --enable-purge-protection \
    --only-show-errors >/dev/null
fi

KEY_VAULT_ID="$(az keyvault show --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"
ensure_keyvault_admin

for site in "${SITES[@]}"; do
  echo "Seeding app settings from $site..."
  seed_site "$site"
done

echo "Key Vault $KEY_VAULT seeded."