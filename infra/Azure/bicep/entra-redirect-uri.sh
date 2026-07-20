#!/usr/bin/env bash
#
# Shared library for registering/removing a SPA redirect URI on the Vestra Entra
# External ID (CIAM) app registration, via Microsoft Graph client-credentials auth.
#
# Source this file, then call:
#   entra_add_redirect_uri    "https://<swa-hostname>/auth/blank"
#   entra_remove_redirect_uri "https://<swa-hostname>/auth/blank"
#
# Parses Graph JSON responses with plain grep/sed rather than jq — jq isn't
# reliably available on every dev machine (confirmed missing on at least one
# Windows/Git Bash setup used for this project), whereas the JSON shapes here
# (a handful of flat string fields, plus a single array-of-strings field) are
# simple enough to parse reliably without it.
#
# Both functions are opt-in and no-op cleanly (print a message, return 0) when:
#   - ENTRA_GRAPH_TENANT_ID / ENTRA_GRAPH_CLIENT_ID / ENTRA_GRAPH_CLIENT_SECRET
#     aren't all set, or
#   - FRONTEND_ENTRA_CLIENT_ID (the target app to update) isn't set.
#
# This lets dev-create.sh/dev-teardown.sh/deploy-frontend.sh call these
# unconditionally — nothing breaks for anyone not using real Entra sign-in.
#
# One-time setup required (in the CIAM tenant, not the main subscription tenant —
# see FRONTEND.md "Automating the redirect URI" for the exact commands):
#   1. Create a dedicated app registration + service principal in the CIAM tenant.
#   2. Grant it the Microsoft Graph APPLICATION permission
#      Application.ReadWrite.OwnedBy, with admin consent.
#   3. Add that service principal as an owner of the target app (FRONTEND_ENTRA_CLIENT_ID)
#      — required for the OwnedBy-scoped permission to actually apply to it.
#   4. Write ENTRA_GRAPH_TENANT_ID / ENTRA_GRAPH_CLIENT_ID / ENTRA_GRAPH_CLIENT_SECRET
#      to infra/Azure/bicep/.env.entra-graph (gitignored — never commit this file)
#      and `source` it before running dev-create.sh / dev-teardown.sh.

_entra_graph_prereqs_ok() {
  if [ -z "${ENTRA_GRAPH_TENANT_ID:-}" ] || [ -z "${ENTRA_GRAPH_CLIENT_ID:-}" ] || [ -z "${ENTRA_GRAPH_CLIENT_SECRET:-}" ]; then
    echo "    (skipping Entra redirect URI update — ENTRA_GRAPH_TENANT_ID/CLIENT_ID/CLIENT_SECRET not set)"
    return 1
  fi
  if [ -z "${FRONTEND_ENTRA_CLIENT_ID:-}" ]; then
    echo "    (skipping Entra redirect URI update — FRONTEND_ENTRA_CLIENT_ID not set, dev stage likely using FakeAuthProvider)"
    return 1
  fi
  return 0
}

# Extracts the string value of a top-level (or single-nested-object) JSON field
# by name from $1, e.g. _entra_json_field "$json" "access_token".
_entra_json_field() {
  echo "$1" | grep -o "\"$2\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -1 | sed -E 's/.*"([^"]*)"$/\1/'
}

# Extracts the "redirectUris" string array under "spa" into newline-separated
# entries on stdout. Empty output for [] or a missing/null "spa".
_entra_json_redirect_uris() {
  local array_content
  array_content="$(echo "$1" | grep -o '"redirectUris"[[:space:]]*:[[:space:]]*\[[^]]*\]' | sed -E 's/.*\[(.*)\]/\1/')"
  [ -z "$array_content" ] && return 0
  echo "$array_content" | sed -E 's/^"//; s/"$//' | sed -E 's/","/\n/g'
}

# Builds a JSON array-of-strings literal from newline-separated input on $1.
_entra_json_build_array() {
  local json="[" first=true uri
  while IFS= read -r uri; do
    [ -z "$uri" ] && continue
    if [ "$first" = true ]; then first=false; else json="${json},"; fi
    json="${json}\"${uri}\""
  done <<< "$1"
  echo "${json}]"
}

# Sets ENTRA_GRAPH_ACCESS_TOKEN or returns 1.
_entra_graph_get_token() {
  local response
  response="$(curl -s -X POST "https://login.microsoftonline.com/${ENTRA_GRAPH_TENANT_ID}/oauth2/v2.0/token" \
    -d "client_id=${ENTRA_GRAPH_CLIENT_ID}" \
    -d "scope=https://graph.microsoft.com/.default" \
    -d "client_secret=${ENTRA_GRAPH_CLIENT_SECRET}" \
    -d "grant_type=client_credentials")"

  ENTRA_GRAPH_ACCESS_TOKEN="$(_entra_json_field "$response" "access_token")"
  if [ -z "$ENTRA_GRAPH_ACCESS_TOKEN" ]; then
    echo "    Failed to acquire Microsoft Graph token: $(_entra_json_field "$response" "error_description")"
    return 1
  fi
  return 0
}

# Looks up the target app's Graph object id (needed for PATCH — distinct from
# FRONTEND_ENTRA_CLIENT_ID, which is the appId) and its current spa.redirectUris.
# Sets ENTRA_GRAPH_APP_OBJECT_ID and ENTRA_GRAPH_APP_REDIRECT_URIS (newline-separated).
_entra_graph_get_app() {
  local response
  response="$(curl -s -X GET \
    "https://graph.microsoft.com/v1.0/applications(appId='${FRONTEND_ENTRA_CLIENT_ID}')?\$select=id,spa" \
    -H "Authorization: Bearer ${ENTRA_GRAPH_ACCESS_TOKEN}")"

  ENTRA_GRAPH_APP_OBJECT_ID="$(_entra_json_field "$response" "id")"
  if [ -z "$ENTRA_GRAPH_APP_OBJECT_ID" ]; then
    echo "    Failed to look up app registration ${FRONTEND_ENTRA_CLIENT_ID}: $(_entra_json_field "$response" "message")"
    return 1
  fi
  ENTRA_GRAPH_APP_REDIRECT_URIS="$(_entra_json_redirect_uris "$response")"
  return 0
}

_entra_graph_patch_redirect_uris() {
  local uris_json="$1"
  local response http_code
  response="$(curl -s -w '\n%{http_code}' -X PATCH \
    "https://graph.microsoft.com/v1.0/applications/${ENTRA_GRAPH_APP_OBJECT_ID}" \
    -H "Authorization: Bearer ${ENTRA_GRAPH_ACCESS_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"spa\":{\"redirectUris\":${uris_json}}}")"
  http_code="$(echo "$response" | tail -n1)"
  if [ "$http_code" != "204" ]; then
    echo "    Graph PATCH failed (status $http_code): $(echo "$response" | sed '$d')"
    return 1
  fi
  return 0
}

# Adds $1 to the app's SPA redirect URIs if not already present. Idempotent.
entra_add_redirect_uri() {
  local new_uri="$1"
  _entra_graph_prereqs_ok || return 0
  _entra_graph_get_token || return 0
  _entra_graph_get_app || return 0

  if echo "$ENTRA_GRAPH_APP_REDIRECT_URIS" | grep -qxF "$new_uri"; then
    echo "    Redirect URI already registered: $new_uri"
    return 0
  fi

  local updated_json
  updated_json="$(_entra_json_build_array "$(printf '%s\n%s' "$ENTRA_GRAPH_APP_REDIRECT_URIS" "$new_uri")")"
  if _entra_graph_patch_redirect_uris "$updated_json"; then
    echo "    Registered redirect URI on Entra app ${FRONTEND_ENTRA_CLIENT_ID}: $new_uri"
  fi
}

# Removes $1 from the app's SPA redirect URIs if present. Idempotent — no-op if absent.
entra_remove_redirect_uri() {
  local old_uri="$1"
  _entra_graph_prereqs_ok || return 0
  _entra_graph_get_token || return 0
  _entra_graph_get_app || return 0

  if ! echo "$ENTRA_GRAPH_APP_REDIRECT_URIS" | grep -qxF "$old_uri"; then
    echo "    Redirect URI not registered (nothing to remove): $old_uri"
    return 0
  fi

  local remaining updated_json
  remaining="$(echo "$ENTRA_GRAPH_APP_REDIRECT_URIS" | grep -vxF "$old_uri")"
  updated_json="$(_entra_json_build_array "$remaining")"
  if _entra_graph_patch_redirect_uris "$updated_json"; then
    echo "    Removed redirect URI from Entra app ${FRONTEND_ENTRA_CLIENT_ID}: $old_uri"
  fi
}
