#!/usr/bin/env bash
#
# Deploys the dev stage end-to-end: resource group, infra (Key Vault, F1 App
# Service, App Insights), throwaway Key Vault secrets, publishes + deploys the
# API, then smoke tests the Key Vault / managed identity / RBAC wiring via the
# unauthenticated POST /api/garments/analyze endpoint.
#
# Does NOT tear anything down — leaves the stage running so you can iterate /
# poke at it. Run dev-teardown.sh when you're done to stop accruing cost.
#
# This Bicep doesn't provision a real Azure OpenAI resource, so dummy secret
# values are enough to prove Key Vault wiring works: the analyze call should
# fail with 502 "Azure OpenAI call failed" (secrets were fetched fine, only
# the fake OpenAI endpoint failed) rather than 500 (Key Vault/identity/RBAC
# broken). Override AOAI_ENDPOINT/AOAI_API_KEY/AOAI_DEPLOYMENT to point at a
# real dev Azure OpenAI resource instead, for a true end-to-end run.
#
# Usage:
#   ./dev-create.sh [--yes]
#
# Prereqs: az CLI (logged in via `az login`, subscription selected via
# `az account set --subscription <id>`), dotnet SDK, and either `zip` or
# PowerShell's Compress-Archive (used automatically as a fallback on Windows).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

RESOURCE_GROUP="rg-robe-dev"
LOCATION="eastus"
KEY_VAULT="kv-robe-dev"
APP_SERVICE="app-robe-dev"
APP_URL="https://${APP_SERVICE}.azurewebsites.net"

AOAI_ENDPOINT="${AOAI_ENDPOINT:-https://test.openai.azure.com}"
AOAI_API_KEY="${AOAI_API_KEY:-test-key}"
AOAI_DEPLOYMENT="${AOAI_DEPLOYMENT:-gpt-4o}"

ASSUME_YES=false
for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=true ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

fail() {
  echo "FAILED: $1" >&2
  echo "Resources may be partially deployed — run ./dev-teardown.sh to clean up." >&2
  exit 1
}

make_zip() {
  local src_dir="$1" zip_path="$2"
  if command -v zip >/dev/null 2>&1; then
    (cd "$src_dir" && zip -r -q "$zip_path" .)
  elif command -v powershell.exe >/dev/null 2>&1; then
    local win_src win_zip
    win_src="$(cygpath -w "$src_dir" 2>/dev/null || echo "$src_dir")"
    win_zip="$(cygpath -w "$zip_path" 2>/dev/null || echo "$zip_path")"
    powershell.exe -NoProfile -Command "Compress-Archive -Path '${win_src}\\*' -DestinationPath '${win_zip}' -Force"
  else
    fail "Neither 'zip' nor 'powershell.exe' is available to build the deployment package."
  fi
}

echo "==> Checking prerequisites..."
command -v az >/dev/null 2>&1 || fail "az CLI not found. Install: https://aka.ms/install-az-cli"
command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK not found."
command -v curl >/dev/null 2>&1 || fail "curl not found."
az account show >/dev/null 2>&1 || fail "Not logged in to Azure. Run: az login"

ACTIVE_SUB="$(az account show --query name -o tsv)"
echo "==> Active subscription: $ACTIVE_SUB"
if [ "$ASSUME_YES" != true ]; then
  read -r -p "Deploy rg-robe-dev to this subscription? [y/N] " CONFIRM
  case "$CONFIRM" in
    y|Y) ;;
    *) echo "Aborted."; exit 0 ;;
  esac
fi

echo "==> [1/6] Creating resource group $RESOURCE_GROUP in $LOCATION..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "==> [2/6] Deploying dev stage infra (Key Vault, App Service, App Insights)..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$SCRIPT_DIR/modules/stage.bicep" \
  --parameters "$SCRIPT_DIR/parameters/dev.bicepparam" \
  --output none

echo "==> [3/6] Populating Key Vault secrets..."
az keyvault secret set --vault-name "$KEY_VAULT" --name "AzureOpenAI--Endpoint" --value "$AOAI_ENDPOINT" --output none
az keyvault secret set --vault-name "$KEY_VAULT" --name "AzureOpenAI--ApiKey" --value "$AOAI_API_KEY" --output none
az keyvault secret set --vault-name "$KEY_VAULT" --name "AzureOpenAI--DeploymentName" --value "$AOAI_DEPLOYMENT" --output none

echo "==> [4/6] Publishing and deploying robe.api..."
PUBLISH_DIR="$(mktemp -d)"
dotnet publish "$REPO_ROOT/robe.api" -c Release -o "$PUBLISH_DIR" --nologo
ZIP_PATH="${PUBLISH_DIR}.zip"
make_zip "$PUBLISH_DIR" "$ZIP_PATH"
az webapp deploy --resource-group "$RESOURCE_GROUP" --name "$APP_SERVICE" --src-path "$ZIP_PATH" --type zip --output none
rm -rf "$PUBLISH_DIR" "$ZIP_PATH"

echo "==> [5/6] Waiting for the app to come up..."
STATUS="000"
for i in $(seq 1 20); do
  STATUS="$(curl -s -o /dev/null -w '%{http_code}' "$APP_URL/swagger/v1/swagger.json" || echo "000")"
  [ "$STATUS" = "200" ] && break
  echo "    ($i/20) swagger not ready yet (status=$STATUS), waiting 10s..."
  sleep 10
done
[ "$STATUS" = "200" ] || fail "App never came up (last status=$STATUS). Check: az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_SERVICE"
echo "    App is up (swagger 200)."

echo "==> [6/6] Smoke testing Key Vault wiring via POST /api/garments/analyze..."
# 1x1 transparent PNG.
TINY_PNG_BASE64="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="

MAX_ATTEMPTS=4
for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  RESPONSE="$(curl -s -w '\n%{http_code}' -X POST "$APP_URL/api/garments/analyze" \
    -H "Content-Type: application/json" \
    -d "{\"imageBase64\":\"$TINY_PNG_BASE64\",\"mimeType\":\"image/png\"}")"
  HTTP_CODE="$(echo "$RESPONSE" | tail -n1)"
  BODY="$(echo "$RESPONSE" | sed '$d')"

  if [ "$HTTP_CODE" = "502" ]; then
    echo "    Got 502 (Azure OpenAI call failed) — expected with dummy secrets."
    echo "    Proves Key Vault secrets were fetched successfully via managed identity + RBAC."
    echo ""
    echo "RESULT: PASS"
    break
  fi

  if [ "$attempt" -lt "$MAX_ATTEMPTS" ]; then
    echo "    ($attempt/$MAX_ATTEMPTS) got $HTTP_CODE, not the expected 502 yet — newly granted RBAC"
    echo "    roles can take a minute to propagate. Retrying in 20s..."
    sleep 20
  else
    echo "$BODY"
    fail "Got $HTTP_CODE after $MAX_ATTEMPTS attempts (expected 502). Likely Key Vault/identity/RBAC — see body above."
  fi
done

echo ""
echo "==> dev stage is up at $APP_URL"
echo "    Run ./dev-teardown.sh when you're done to stop accruing cost."
