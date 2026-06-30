#!/usr/bin/env bash
#
# Tears down everything dev-create.sh deployed. Deletes the rg-robe-dev
# resource group (Key Vault, App Service, Plan, App Insights) in one shot.
#
# Dev has Key Vault purge protection OFF, so the deleted vault soft-deletes
# instead of disappearing immediately — the name kv-robeai-dev stays reserved
# for up to 90 days unless purged. Pass --purge-vault to reclaim the name
# right away (only meaningful if you plan to redeploy soon).
#
# Usage:
#   ./dev-teardown.sh [--yes] [--wait] [--purge-vault]

set -euo pipefail

RESOURCE_GROUP="rg-robe-dev"
LOCATION="canadacentral"
KEY_VAULT="kv-robeai-dev"

ASSUME_YES=false
WAIT_FOR_DELETE=false
PURGE_VAULT=false

for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=true ;;
    --wait) WAIT_FOR_DELETE=true ;;
    --purge-vault) PURGE_VAULT=true ;;
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

if [ "$WAIT_FOR_DELETE" = true ]; then
  echo "==> Deleting $RESOURCE_GROUP (waiting for completion)..."
  az group delete --name "$RESOURCE_GROUP" --yes
  echo "==> Deleted."
else
  echo "==> Deleting $RESOURCE_GROUP (not waiting — runs in the background)..."
  az group delete --name "$RESOURCE_GROUP" --yes --no-wait
fi

if [ "$PURGE_VAULT" = true ]; then
  if [ "$WAIT_FOR_DELETE" != true ]; then
    echo "==> Waiting for the resource group delete to finish first (purge needs the vault gone)..."
    az group wait --name "$RESOURCE_GROUP" --deleted
  fi
  echo "==> Purging soft-deleted Key Vault $KEY_VAULT to reclaim the name immediately..."
  az keyvault purge --name "$KEY_VAULT" --location "$LOCATION" || \
    echo "    Purge failed or nothing to purge — it may already be gone, or still finishing deletion (retry in a minute)."
fi
