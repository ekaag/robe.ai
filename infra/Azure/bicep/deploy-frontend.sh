#!/usr/bin/env bash
#
# Builds the Vestra Next.js frontend and deploys it to the Azure Static Web App
# for a given stage (dev | gamma | live).
#
# Prereq: the stage's Bicep infra must already be deployed — specifically the
# SWA resource (swa-robe-<stage>) and App Service (app-robe-<stage>) must exist.
# For dev, dev-create.sh handles the full end-to-end (Bicep + backend + frontend).
# Use this script to redeploy just the frontend, or to deploy gamma/live after
# running main.bicep or stage.bicep directly.
#
# Usage:
#   ./deploy-frontend.sh --stage=dev|gamma|live [--yes] [--subscription=<id>]
#
# The API endpoint is derived from the stage name:
#   dev   → https://app-robe-dev.azurewebsites.net
#   gamma → https://app-robe-gamma.azurewebsites.net
#   live  → https://app-robe-live.azurewebsites.net
#
# Entra vars are optional. If not set, the frontend uses FakeAuthProvider
# (pre-authenticated dev-user, no sign-in screen). Set them in your shell before
# running for a stage that needs real Entra sign-in:
#   export FRONTEND_ENTRA_AUTHORITY="https://vestraoauth.ciamlogin.com/vestraoauth.onmicrosoft.com"
#   export FRONTEND_ENTRA_CLIENT_ID="d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9"
#   export FRONTEND_ENTRA_API_SCOPE="api://d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9/access_as_user"
# NEXT_PUBLIC_ENTRA_REDIRECT_URI is derived automatically from the SWA hostname.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
FRONTEND_DIR="$REPO_ROOT/vestra/apps/web"

STAGE=""
ASSUME_YES=false
SUBSCRIPTION=""

for arg in "$@"; do
  case "$arg" in
    --stage=*) STAGE="${arg#*=}" ;;
    --yes|-y) ASSUME_YES=true ;;
    --subscription=*) SUBSCRIPTION="${arg#*=}" ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

if [ -z "$STAGE" ]; then
  echo "Usage: $0 --stage=dev|gamma|live [--yes] [--subscription=<id>]" >&2
  exit 1
fi
case "$STAGE" in
  dev|gamma|live) ;;
  *) echo "Invalid stage '$STAGE'. Must be dev, gamma, or live." >&2; exit 1 ;;
esac

RESOURCE_GROUP="rg-robe-${STAGE}"
SWA_NAME="swa-robe-${STAGE}"
APP_URL="https://app-robe-${STAGE}.azurewebsites.net"

fail() { echo "FAILED: $1" >&2; exit 1; }

# ── Prerequisites ─────────────────────────────────────────────────────────────

echo "==> Checking prerequisites..."
command -v az >/dev/null 2>&1 || fail "az CLI not found. Install from https://aka.ms/install-az-cli"
command -v node >/dev/null 2>&1 || fail "Node.js not found."
command -v pnpm >/dev/null 2>&1 || { echo "    pnpm not found — installing globally via npm..."; npm install -g pnpm; }
az account show >/dev/null 2>&1 || fail "Not logged in to Azure. Run: az login"

if [ -n "$SUBSCRIPTION" ]; then
  az account set --subscription "$SUBSCRIPTION" --output none \
    || fail "Could not switch to subscription '$SUBSCRIPTION'."
fi

ACTIVE_SUB="$(az account show --query name -o tsv)"
echo "    Subscription : $ACTIVE_SUB"
echo "    Stage        : $STAGE"
echo "    Resource group: $RESOURCE_GROUP"
echo "    SWA          : $SWA_NAME"
echo "    API endpoint : $APP_URL"

if [ "$ASSUME_YES" != true ]; then
  read -r -p "Deploy frontend to $SWA_NAME ($STAGE) in '$ACTIVE_SUB'? [y/N] " CONFIRM
  case "$CONFIRM" in
    y|Y) ;;
    *) echo "Aborted."; exit 0 ;;
  esac
fi

# ── Step 1: build ─────────────────────────────────────────────────────────────

echo "==> [1/2] Building Next.js standalone..."

# Read the SWA hostname so NEXT_PUBLIC_ENTRA_REDIRECT_URI is correct for this stage.
SWA_HOSTNAME="$(az staticwebapp show \
  --name "$SWA_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "defaultHostname" \
  -o tsv 2>/dev/null)" \
  || fail "SWA '$SWA_NAME' not found in '$RESOURCE_GROUP'. Deploy the Bicep infra first."
[ -n "$SWA_HOSTNAME" ] \
  || fail "Could not read hostname from $SWA_NAME. Verify it exists in $RESOURCE_GROUP."
echo "    SWA hostname: $SWA_HOSTNAME"

cd "$REPO_ROOT/vestra"
pnpm install --frozen-lockfile

# NEXT_PUBLIC_* vars are baked into the JS bundle at build time.
# Entra vars are optional — if not set, the deployed frontend uses FakeAuthProvider.
# For gamma/live you almost certainly want real Entra; set the three shell vars above.
export NEXT_PUBLIC_API_BASE_URL="$APP_URL"
export NEXT_PUBLIC_ENTRA_REDIRECT_URI="https://${SWA_HOSTNAME}/auth/blank"
export NEXT_PUBLIC_ENTRA_AUTHORITY="${FRONTEND_ENTRA_AUTHORITY:-}"
export NEXT_PUBLIC_ENTRA_CLIENT_ID="${FRONTEND_ENTRA_CLIENT_ID:-}"
export NEXT_PUBLIC_ENTRA_API_SCOPE="${FRONTEND_ENTRA_API_SCOPE:-}"
echo "==> next.config.mjs output field:"
grep -E 'output|standalone' "$FRONTEND_DIR/next.config.mjs" || echo "(not found)"

pnpm --filter @vestra/web run build

echo "==> .next/ contents after build:"
ls -la "$FRONTEND_DIR/.next/" || echo "(no .next dir)"
echo "==> .next/standalone/ contents:"
ls -la "$FRONTEND_DIR/.next/standalone/" || echo "(no standalone dir)"
echo "==> .next/standalone/ tree (2 levels):"
find "$FRONTEND_DIR/.next/standalone" -maxdepth 2 -not -path "*/node_modules/*" | sort || true

# Next.js standalone output does not include static assets or public/ — copy them in.
cp -r "$FRONTEND_DIR/.next/static"  "$FRONTEND_DIR/.next/standalone/.next/static"
[ -d "$FRONTEND_DIR/public" ] \
  && cp -r "$FRONTEND_DIR/public" "$FRONTEND_DIR/.next/standalone/public"
cp "$FRONTEND_DIR/staticwebapp.config.json" "$FRONTEND_DIR/.next/standalone/staticwebapp.config.json"

# ── Step 2: deploy ────────────────────────────────────────────────────────────

echo "==> [2/2] Deploying to $SWA_NAME..."

SWA_TOKEN="$(az staticwebapp secrets list \
  --name "$SWA_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.apiKey" \
  -o tsv)"
[ -n "$SWA_TOKEN" ] || fail "Could not retrieve deployment token for $SWA_NAME."

# --skip-app-build: we already built above with our custom env vars and pnpm workspace.
npx --yes @azure/static-web-apps-cli@latest deploy \
  --app-location "$FRONTEND_DIR/.next/standalone" \
  --skip-app-build \
  --deployment-token "$SWA_TOKEN"

echo ""
echo "==> Done. $STAGE frontend is live at: https://$SWA_HOSTNAME"
