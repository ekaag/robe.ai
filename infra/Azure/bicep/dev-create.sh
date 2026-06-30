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
#   ./dev-create.sh [--yes] [--subscription=<id-or-name>]
#
# If az CLI isn't found, you'll be asked for consent before this script
# installs it (winget on Windows, brew on macOS, Microsoft's install script on
# Linux) — this happens even with --yes, since installing software is a
# bigger ask than skipping a confirmation prompt.
#
# If --subscription isn't given and --yes isn't set, you'll be shown your
# available subscriptions and asked to pick one; otherwise the currently
# active subscription (`az account show`) is used as-is.
#
# Prereqs: az CLI (logged in via `az login`), dotnet SDK, and either `zip` or
# PowerShell's Compress-Archive (used automatically as a fallback on Windows).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

RESOURCE_GROUP="rg-robe-dev"
LOCATION="canadacentral"
KEY_VAULT="kv-robeai-dev"
APP_SERVICE="app-robe-dev"
APP_URL="https://${APP_SERVICE}.azurewebsites.net"

AOAI_ENDPOINT="${AOAI_ENDPOINT:-https://test.openai.azure.com}"
AOAI_API_KEY="${AOAI_API_KEY:-test-key}"
AOAI_DEPLOYMENT="${AOAI_DEPLOYMENT:-gpt-4o}"

ASSUME_YES=false
SUBSCRIPTION=""
for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=true ;;
    --subscription=*) SUBSCRIPTION="${arg#*=}" ;;
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

# Installs az CLI if missing, always asking for explicit consent first — even
# under --yes, since installing software is a bigger ask than skipping a
# confirmation prompt. On GitHub-hosted runners az is preinstalled, so this
# is a no-op in CI.
ensure_az_cli() {
  if command -v az >/dev/null 2>&1; then
    return
  fi

  echo "az CLI not found."
  read -r -p "Install Azure CLI now? [y/N] " INSTALL_CONFIRM
  case "$INSTALL_CONFIRM" in
    y|Y) ;;
    *) fail "az CLI is required. Install manually: https://aka.ms/install-az-cli" ;;
  esac

  echo "==> Installing Azure CLI..."
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      if command -v winget >/dev/null 2>&1; then
        winget install --exact --id Microsoft.AzureCLI --source winget \
          --accept-package-agreements --accept-source-agreements
      else
        fail "winget not found. Install Azure CLI manually: https://aka.ms/installazurecliwindows"
      fi
      ;;
    Darwin*)
      command -v brew >/dev/null 2>&1 || fail "Homebrew not found. Install Azure CLI manually: https://learn.microsoft.com/cli/azure/install-azure-cli-macos"
      brew update && brew install azure-cli
      ;;
    Linux*)
      curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash || \
        fail "Automatic install failed. Install manually: https://aka.ms/install-az-cli"
      ;;
    *)
      fail "Unsupported OS for automatic install. Install Azure CLI manually: https://aka.ms/install-az-cli"
      ;;
  esac

  hash -r 2>/dev/null || true
  command -v az >/dev/null 2>&1 || \
    fail "az CLI was installed but isn't on PATH in this shell yet. Open a new terminal and re-run this script."
  echo "==> Azure CLI installed."
}

# Picks the subscription to deploy into: --subscription overrides everything;
# otherwise --yes keeps whatever's currently active; otherwise shows a
# numbered list from `az account list` and asks.
select_subscription() {
  if [ -n "$SUBSCRIPTION" ]; then
    az account set --subscription "$SUBSCRIPTION" --output none || \
      fail "Could not switch to subscription '$SUBSCRIPTION'."
    ACTIVE_SUB="$(az account show --query name -o tsv)"
    echo "==> Using subscription: $ACTIVE_SUB"
    return
  fi

  if [ "$ASSUME_YES" = true ]; then
    ACTIVE_SUB="$(az account show --query name -o tsv)"
    echo "==> Using current subscription: $ACTIVE_SUB"
    return
  fi

  local sub_list
  sub_list="$(az account list --query "[].{name:name, id:id, isDefault:isDefault}" -o tsv)"
  [ -n "$sub_list" ] || fail "No subscriptions found for this account."

  echo "==> Available subscriptions:"
  local -a sub_names sub_ids
  local i=1 sub_name sub_id sub_default marker
  while IFS=$'\t' read -r sub_name sub_id sub_default; do
    marker=""
    [ "$sub_default" = "True" ] && marker=" (current)"
    echo "  [$i] $sub_name  ($sub_id)$marker"
    sub_names[i]="$sub_name"
    sub_ids[i]="$sub_id"
    i=$((i + 1))
  done <<< "$sub_list"

  read -r -p "Pick a subscription number [Enter = keep current]: " PICK
  if [ -z "$PICK" ]; then
    ACTIVE_SUB="$(az account show --query name -o tsv)"
    echo "==> Keeping current subscription: $ACTIVE_SUB"
    return
  fi
  if ! [[ "$PICK" =~ ^[0-9]+$ ]] || [ "$PICK" -lt 1 ] || [ "$PICK" -ge "$i" ]; then
    fail "Invalid selection: $PICK"
  fi

  az account set --subscription "${sub_ids[$PICK]}" --output none
  ACTIVE_SUB="${sub_names[$PICK]}"
  echo "==> Switched to subscription: $ACTIVE_SUB (${sub_ids[$PICK]})"
}

echo "==> Checking prerequisites..."
ensure_az_cli
command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK not found."
command -v curl >/dev/null 2>&1 || fail "curl not found."
az account show >/dev/null 2>&1 || fail "Not logged in to Azure. Run: az login"

select_subscription
if [ "$ASSUME_YES" != true ]; then
  read -r -p "Deploy rg-robe-dev to $ACTIVE_SUB? [y/N] " CONFIRM
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
