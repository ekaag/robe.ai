#!/usr/bin/env bash
#
# Deploys the dev stage end-to-end:
#   1. Creates resource group
#   2. Deploys infra via Bicep: Key Vault, App Service (with managed identity),
#      Azure OpenAI account + gpt-5-mini deployment, RBAC grants, App Insights
#   3. Grants the CLI caller Key Vault Secrets Officer (to write secrets)
#   4. Reads the OpenAI endpoint from Bicep outputs and writes both OpenAI
#      secrets into Key Vault (no API key — managed identity authenticates)
#   5. Publishes and deploys robe.api via zip deploy
#   6. Waits for the app to come up
#   7. Smoke tests the full wiring: Key Vault → managed identity → Azure OpenAI
#      A real OpenAI call is made; success is 200 (traits extracted) or 422
#      (no garment detected in the tiny PNG) — both prove the round-trip works.
#
# Does NOT tear anything down — leaves the stage running so you can iterate.
# Run dev-teardown.sh when you're done to stop accruing cost.
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
SWA_NAME="swa-robe-dev"
DEPLOY_NAME="robe-dev-infra"
FRONTEND_DIR="$REPO_ROOT/vestra/apps/web"

ASSUME_YES=false
SUBSCRIPTION=""
SKIP_FRONTEND=false
for arg in "$@"; do
  case "$arg" in
    --yes|-y) ASSUME_YES=true ;;
    --subscription=*) SUBSCRIPTION="${arg#*=}" ;;
    --skip-frontend) SKIP_FRONTEND=true ;;
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
if [ "$SKIP_FRONTEND" != true ]; then
  command -v node >/dev/null 2>&1 || fail "Node.js not found. Install it to build the frontend, or pass --skip-frontend."
  command -v pnpm >/dev/null 2>&1 || { echo "pnpm not found — installing globally via npm..."; npm install -g pnpm; }
fi
az account show >/dev/null 2>&1 || fail "Not logged in to Azure. Run: az login"

select_subscription
if [ "$ASSUME_YES" != true ]; then
  read -r -p "Deploy rg-robe-dev to $ACTIVE_SUB? [y/N] " CONFIRM
  case "$CONFIRM" in
    y|Y) ;;
    *) echo "Aborted."; exit 0 ;;
  esac
fi

echo "==> [1/7] Creating resource group $RESOURCE_GROUP in $LOCATION..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "==> [2/7] Deploying dev stage infra (Key Vault, App Service, Azure OpenAI, App Insights)..."
# Named deployment so we can read its outputs in the next step.
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOY_NAME" \
  --template-file "$SCRIPT_DIR/modules/stage.bicep" \
  --parameters "$SCRIPT_DIR/parameters/dev.bicepparam" \
  --output none

echo "    Reading OpenAI endpoint from deployment outputs..."
AOAI_ENDPOINT="$(az deployment group show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOY_NAME" \
  --query "properties.outputs.openAiEndpoint.value" \
  -o tsv)"
AOAI_DEPLOYMENT="$(az deployment group show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOY_NAME" \
  --query "properties.outputs.openAiDeploymentName.value" \
  -o tsv)"
[ -n "$AOAI_ENDPOINT" ] || fail "OpenAI endpoint not found in deployment outputs."
echo "    OpenAI endpoint: $AOAI_ENDPOINT  deployment: $AOAI_DEPLOYMENT"

echo "==> [3/7] Ensuring caller can write Key Vault secrets..."
# kv-robeai-dev uses RBAC authorization (enableRbacAuthorization: true), and
# stage.bicep only grants data-plane access to the App Service's managed
# identity (read-only "Key Vault Secrets User"). Management-plane roles like
# Owner/Contributor do NOT imply Key Vault data-plane access under RBAC mode,
# so the signed-in CLI user needs an explicit "Key Vault Secrets Officer"
# grant before `az keyvault secret set` below will succeed.
USER_OBJECT_ID="$(az ad signed-in-user show --query id -o tsv)"
VAULT_ID="$(az keyvault show --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"
# MSYS_NO_PATHCONV=1 is required on Git Bash/Windows: MSYS auto-converts any
# argument starting with "/" into a Windows path before exec'ing a native
# .exe/.cmd like az, which silently mangles "--scope /subscriptions/..." and
# breaks the request (surfaces as a confusing "(MissingSubscription)" error).
# Scoped to just these two calls — other az calls in this script (e.g.
# --template-file, --src-path) rely on that same conversion for real local
# file paths, so it must not be disabled globally.
EXISTING_ASSIGNMENT="$(MSYS_NO_PATHCONV=1 az role assignment list --assignee "$USER_OBJECT_ID" --scope "$VAULT_ID" --role "Key Vault Secrets Officer" --query "[0].id" -o tsv)"
if [ -z "$EXISTING_ASSIGNMENT" ]; then
  MSYS_NO_PATHCONV=1 az role assignment create --role "Key Vault Secrets Officer" \
    --assignee-object-id "$USER_OBJECT_ID" --assignee-principal-type User \
    --scope "$VAULT_ID" --output none
  echo "    Granted Key Vault Secrets Officer. Waiting 30s for RBAC propagation..."
  sleep 30
else
  echo "    Already granted."
fi

echo "==> [4/7] Writing OpenAI secrets to Key Vault..."
# The endpoint comes from Bicep outputs (read in step 2); no API key is stored
# because the App Service managed identity authenticates via the
# "Cognitive Services OpenAI User" RBAC grant that stage.bicep already created.
az keyvault secret set --vault-name "$KEY_VAULT" --name "AzureOpenAI--Endpoint" --value "$AOAI_ENDPOINT" --output none
az keyvault secret set --vault-name "$KEY_VAULT" --name "AzureOpenAI--DeploymentName" --value "$AOAI_DEPLOYMENT" --output none

echo "==> [5/7] Publishing and deploying robe.api..."
PUBLISH_DIR="$(mktemp -d)"
dotnet publish "$REPO_ROOT/robe.api" -c Release -o "$PUBLISH_DIR" --nologo
ZIP_PATH="${PUBLISH_DIR}.zip"
make_zip "$PUBLISH_DIR" "$ZIP_PATH"
az webapp deploy --resource-group "$RESOURCE_GROUP" --name "$APP_SERVICE" --src-path "$ZIP_PATH" --type zip --output none
rm -rf "$PUBLISH_DIR" "$ZIP_PATH"

echo "==> [6/7] Waiting for the app to come up..."
STATUS="000"
for i in $(seq 1 20); do
  STATUS="$(curl -s -o /dev/null -w '%{http_code}' "$APP_URL/swagger/v1/swagger.json" || echo "000")"
  [ "$STATUS" = "200" ] && break
  echo "    ($i/20) swagger not ready yet (status=$STATUS), waiting 10s..."
  sleep 10
done
[ "$STATUS" = "200" ] || fail "App never came up (last status=$STATUS). Check: az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_SERVICE"
echo "    App is up (swagger 200)."

echo "==> [7/7] Smoke testing full wiring: Key Vault → managed identity → Azure OpenAI..."
# 1x1 transparent PNG — valid image bytes, passes API validation.
# Expected outcomes (both prove the full round-trip worked):
#   200 — model extracted traits (unlikely for a 1x1 but possible)
#   422 — model found no garment/person in the image (most likely)
# Any 5xx would indicate Key Vault / RBAC / OpenAI wiring failure.
TINY_PNG_BASE64="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="

MAX_ATTEMPTS=4
for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  RESPONSE="$(curl -s -w '\n%{http_code}' -X POST "$APP_URL/api/garments/analyze" \
    -H "Content-Type: application/json" \
    -d "{\"imageBase64\":\"$TINY_PNG_BASE64\",\"mimeType\":\"image/png\"}")"
  HTTP_CODE="$(echo "$RESPONSE" | tail -n1)"
  BODY="$(echo "$RESPONSE" | sed '$d')"

  if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "422" ]; then
    echo "    Got $HTTP_CODE — Azure OpenAI call succeeded."
    echo "    Key Vault secrets, managed identity, and Cognitive Services OpenAI User RBAC all verified."
    echo ""
    echo "RESULT: PASS"
    break
  fi

  if [ "$attempt" -lt "$MAX_ATTEMPTS" ]; then
    echo "    ($attempt/$MAX_ATTEMPTS) got $HTTP_CODE — RBAC grants can take a minute to propagate. Retrying in 20s..."
    sleep 20
  else
    echo "$BODY"
    fail "Got $HTTP_CODE after $MAX_ATTEMPTS attempts (expected 200 or 422). Check body above and app logs: az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_SERVICE"
  fi
done

if [ "$SKIP_FRONTEND" = true ]; then
  echo "==> Skipping frontend build/deploy (--skip-frontend)."
else
  echo "==> [8/9] Building frontend (Next.js standalone)..."
  # Read the SWA hostname from deployment outputs so NEXT_PUBLIC_ENTRA_REDIRECT_URI is correct.
  SWA_HOSTNAME="$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOY_NAME" \
    --query "properties.outputs.staticWebAppHostName.value" \
    -o tsv)"
  [ -n "$SWA_HOSTNAME" ] || fail "staticWebAppHostName not found in deployment outputs."
  echo "    SWA hostname: $SWA_HOSTNAME"

  # NEXT_PUBLIC_* vars are baked in at build time by the Next.js compiler.
  # Entra vars are optional for the dev stage (the backend falls back to LocalAuthHandler
  # when Entra:Authority is not configured). Export them from your shell env before running
  # this script if you need real sign-in:
  #   export FRONTEND_ENTRA_AUTHORITY="https://vestraoauth.ciamlogin.com/..."
  #   export FRONTEND_ENTRA_CLIENT_ID="d4cd6bf9-..."
  #   export FRONTEND_ENTRA_API_SCOPE="api://d4cd6bf9-.../access_as_user"
  cd "$REPO_ROOT/vestra"
  pnpm install --frozen-lockfile

  NEXT_PUBLIC_API_BASE_URL="$APP_URL" \
  NEXT_PUBLIC_ENTRA_REDIRECT_URI="https://${SWA_HOSTNAME}/auth/blank" \
  NEXT_PUBLIC_ENTRA_AUTHORITY="${FRONTEND_ENTRA_AUTHORITY:-}" \
  NEXT_PUBLIC_ENTRA_CLIENT_ID="${FRONTEND_ENTRA_CLIENT_ID:-}" \
  NEXT_PUBLIC_ENTRA_API_SCOPE="${FRONTEND_ENTRA_API_SCOPE:-}" \
    pnpm --filter @vestra/web run build

  # Bundle static assets into the standalone output so the Node.js server can serve them.
  cp -r "$FRONTEND_DIR/.next/static" "$FRONTEND_DIR/.next/standalone/.next/static"
  if [ -d "$FRONTEND_DIR/public" ]; then
    cp -r "$FRONTEND_DIR/public" "$FRONTEND_DIR/.next/standalone/public"
  fi
  # SWA CLI picks up staticwebapp.config.json from the deployment root.
  cp "$FRONTEND_DIR/staticwebapp.config.json" "$FRONTEND_DIR/.next/standalone/staticwebapp.config.json"

  echo "==> [9/9] Deploying frontend to Azure Static Web Apps ($SWA_NAME)..."
  SWA_TOKEN="$(az staticwebapp secrets list \
    --name "$SWA_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.apiKey" \
    -o tsv)"
  [ -n "$SWA_TOKEN" ] || fail "Could not retrieve SWA deployment token for $SWA_NAME."

  # Deploy the pre-built standalone output. --skip-app-build skips rebuilding inside
  # the SWA CLI since we already built above with our custom env vars and pnpm workspace.
  npx --yes @azure/static-web-apps-cli@latest deploy \
    --app-location "$FRONTEND_DIR/.next/standalone" \
    --skip-app-build \
    --deployment-token "$SWA_TOKEN"

  echo ""
  echo "    Frontend deployed to: https://$SWA_HOSTNAME"
fi

echo ""
echo "==> dev stage is up at $APP_URL"
if [ "$SKIP_FRONTEND" != true ]; then
  echo "    Frontend: https://$SWA_HOSTNAME"
fi
echo "    Run ./dev-teardown.sh when you're done to stop accruing cost."
