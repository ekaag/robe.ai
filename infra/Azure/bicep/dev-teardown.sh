#!/usr/bin/env bash
#
# Tears down everything dev-create.sh deployed. Deletes the rg-robe-dev
# resource group (Key Vault, App Service, Plan, App Insights, Azure OpenAI,
# Cosmos DB, Storage account) in one shot. Cosmos DB and Storage have no
# soft-delete/purge step (unlike Key Vault and Azure OpenAI below) — they're
# gone for good as soon as the resource group delete completes.
#
# Dev has Key Vault purge protection OFF, so the deleted vault soft-deletes
# instead of disappearing immediately — the name kv-robeai-dev stays reserved
# for up to 90 days unless purged. Pass --purge-vault to reclaim the name
# right away (only meaningful if you plan to redeploy soon).
#
# Azure OpenAI accounts have the same 48-hour soft-delete on the custom
# subdomain name ('aoai-robe-dev'). Pass --purge-openai to reclaim it
# immediately so dev-create.sh can redeploy without a name conflict.
# The script auto-detects whichever region the account was deployed in,
# so this works regardless of whether openAiLocation was changed between runs.
#
# If real Entra sign-in was configured for this stage (see deploy-frontend.sh),
# the SWA's redirect URI is also deregistered from the Entra app registration before
# deletion — see entra-redirect-uri.sh for the required ENTRA_GRAPH_* env vars.
#
# Usage:
#   ./dev-teardown.sh [--yes] [--wait] [--purge-vault] [--purge-openai]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

RESOURCE_GROUP="rg-robe-dev"
LOCATION="canadacentral"
KEY_VAULT="kv-robeai-dev"
OPENAI_ACCOUNT="aoai-robe-dev"
SWA_NAME="swa-robe-dev"

ASSUME_YES=false
WAIT_FOR_DELETE=false
PURGE_VAULT=false
PURGE_OPENAI=false

for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=true ;;
    --wait) WAIT_FOR_DELETE=true ;;
    --purge-vault) PURGE_VAULT=true ;;
    --purge-openai) PURGE_OPENAI=true ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

fail() {
  echo "FAILED: $1" >&2
  exit 1
}

command -v az >/dev/null 2>&1 || fail "az CLI not found. Install: https://aka.ms/install-az-cli"
az account show >/dev/null 2>&1 || fail "Not logged in to Azure. Run: az login"

if ! az group show --name "$RESOURCE_GROUP" >/dev/null 2>&1; then
  echo "==> $RESOURCE_GROUP does not exist — nothing to tear down."
  exit 0
fi

if [ "$ASSUME_YES" != true ]; then
  read -r -p "Delete resource group $RESOURCE_GROUP (and everything in it)? [y/N] " CONFIRM
  case "$CONFIRM" in
    y|Y) ;;
    *) echo "Aborted."; exit 0 ;;
  esac
fi

# Read the SWA hostname (if it exists) and deregister its redirect URI from Entra
# *before* deleting anything — once the resource group is gone there's no way to
# recover which hostname this stage was using. No-op if Entra automation isn't
# configured (see entra-redirect-uri.sh) or the SWA never got this far.
SWA_HOSTNAME="$(az staticwebapp show --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP" \
  --query "defaultHostname" -o tsv 2>/dev/null || echo "")"
if [ -n "$SWA_HOSTNAME" ]; then
  echo "==> Deregistering redirect URI from Entra (no-op if ENTRA_GRAPH_* env vars aren't set)..."
  # shellcheck source=entra-redirect-uri.sh
  source "$SCRIPT_DIR/entra-redirect-uri.sh"
  entra_remove_redirect_uri "https://${SWA_HOSTNAME}/auth/blank"
fi

if [ "$WAIT_FOR_DELETE" = true ]; then
  echo "==> Deleting $RESOURCE_GROUP (waiting for completion)..."
  az group delete --name "$RESOURCE_GROUP" --yes
  echo "==> Deleted."
else
  echo "==> Deleting $RESOURCE_GROUP (not waiting — runs in the background)..."
  az group delete --name "$RESOURCE_GROUP" --yes --no-wait
fi

# Both purge steps need the resource group gone first — wait once if either is requested.
needs_wait=false
[ "$PURGE_VAULT" = true ] && needs_wait=true
[ "$PURGE_OPENAI" = true ] && needs_wait=true

if [ "$needs_wait" = true ] && [ "$WAIT_FOR_DELETE" != true ]; then
  echo "==> Waiting for resource group delete to finish before purging..."
  az group wait --name "$RESOURCE_GROUP" --deleted 2>/dev/null || true
fi

if [ "$PURGE_VAULT" = true ]; then
  echo "==> Purging soft-deleted Key Vault $KEY_VAULT to reclaim the name immediately..."
  az keyvault purge --name "$KEY_VAULT" --location "$LOCATION" || \
    echo "    Purge failed or nothing to purge — it may already be gone, or still finishing deletion (retry in a minute)."
fi

if [ "$PURGE_OPENAI" = true ]; then
  echo "==> Looking for soft-deleted Azure OpenAI account $OPENAI_ACCOUNT..."
  # The account may have been deployed in a different region (openAiLocation param) than
  # the rest of the stage — auto-detect from the deleted-accounts list rather than assuming.
  DELETED_LOCATION="$(az cognitiveservices account list-deleted \
    --query "[?name=='$OPENAI_ACCOUNT'].location | [0]" -o tsv 2>/dev/null || echo "")"
  if [ -n "$DELETED_LOCATION" ]; then
    echo "==> Purging soft-deleted Azure OpenAI account $OPENAI_ACCOUNT (location: $DELETED_LOCATION)..."
    az cognitiveservices account purge \
      --name "$OPENAI_ACCOUNT" \
      --resource-group "$RESOURCE_GROUP" \
      --location "$DELETED_LOCATION" || \
      echo "    Purge failed — it may already be gone, or still finishing deletion (retry in a minute)."
  else
    echo "    No soft-deleted account found for $OPENAI_ACCOUNT — nothing to purge."
  fi
fi
