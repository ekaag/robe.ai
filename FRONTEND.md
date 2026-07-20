# Frontend: VESTRA clients (web + iOS + Android)

Companion to `CLAUDE.md` (backend). These clients consume the backend APIs;
they hold no business logic beyond presentation, auth, and orchestration.
Stack: **React (web) + React Native via Expo (iOS/Android)** in a shared
monorepo, so design tokens, API client, types, and auth logic are written once.

---

## Build order (one feature at a time)

0. **Scaffold** — monorepo, design tokens, shared types  ✅
1a. **Auth scaffolding** — `IAuthProvider` + `FakeAuthProvider`, sign-in screen
    (4 ProviderButtons), api-client auth interceptor, session/route gating; all
    tested against the fake. No real Entra. Buildable regardless of Entra setup.  ✅
1b. **Real Entra wiring** — `WebMsalAuthProvider` + `NativeMsalAuthProvider`,
    configure the 4 providers in Entra, verify real login → token → protected
    endpoint (`GET /api/me` or `/api/users/me/profile`) on web + mobile.
    Prereq: live Entra External ID tenant + app registrations + provider creds
    (you set these up in Entra + each provider's portal).  ⬜
2. **Wardrobe + Garment detail** — list, upload, trait view (backend APIs 1–2)  ✅
3. **Style profile** — generate + view (backend API 3)  ✅
4. **Recommendations** — ranked shop list with filters (backend API 4)  ✅
5+ Event suggestions, etc. — follow once the backend APIs exist.

Same cadence as the backend: build one, test it, get sign-off, move on. Flip ⬜
to ✅ here as you finish each.

---

## Working rules (read every session)

- Build **ONE** feature at a time; don't start the next until I approve.
- **Plan first**: show the component tree, the data hooks it needs, and the test
  approach before writing code. Wait for my "go".
- **Share, don't duplicate**: tokens, types, API client, and auth live in
  `packages/*` and are imported by both apps. Platform-specific code only in
  `apps/web` and `apps/mobile`.
- **Interface-first** (mirrors the backend): the API client and the auth layer
  each sit behind an interface, so tests, Storybook, and offline dev use a mock
  implementation instead of hitting the network. Components depend on hooks, not
  on `fetch`.
- Components are presentational and take data via props/hooks — no inline API
  calls inside screens.
- Match the design tokens below exactly; they come from the approved mock.

---

## Monorepo layout

```
vestra/
  apps/
    web/        Next.js 14.2 (App Router) — marketing + authed app
    mobile/     Expo (Expo Router) — iOS + Android
  packages/
    tokens/     design tokens (colors, type, spacing) as TS — one source of truth
    types/      TS types mirroring backend DTOs (GarmentTraits, Garment, ...)
    api/        typed API client + TanStack Query hooks, behind IApiClient
                peerDeps: react; devDeps: @types/react, react, vitest
    auth/       MSAL wrapper behind IAuthProvider (web + native impls)
                peerDeps: react; devDeps: @types/react, react, vitest
    ui-core/    cross-platform primitives (tokens-driven) — optional
```

Tooling: **pnpm workspaces + Turborepo**. TypeScript everywhere, strict mode.

> Note on shared UI: web (DOM) and React Native render differently, so true
> component sharing needs React Native Web or Tamagui. Recommended pragmatic
> split: **always share** tokens + types + api + auth; build **platform-specific
> components** in each app, kept visually identical via the shared tokens. Adopt
> Tamagui/RN-Web later only if component-level reuse becomes worth it.

---

## Auth: Entra External ID via MSAL

All four logins federate through **Microsoft Entra External ID**, which issues
the JWT the backend validates. You integrate one system; the four providers are
configured in the Entra admin center, not in client code.

Flow (identical on every platform):
1. App starts an Entra user flow with **PKCE** via MSAL.
2. User taps a provider (Apple / Google / Microsoft / Facebook). Social
   providers complete in a **browser-delegated (system web-view) flow** — use
   the system browser, not an embedded webview (Google blocks embedded webviews,
   and Apple Store rules require Sign in with Apple, which Entra provides).
3. Entra returns an ID token + access token.
4. The access token is attached as `Authorization: Bearer <token>` on every API
   call. The backend derives `userId` from the token's subject claim — never
   from the request body.

Libraries:
- **Web**: `@azure/msal-browser` + `@azure/msal-react` (auth-code + PKCE).
- **Mobile (Expo)**: `expo-auth-session` + `expo-web-browser` against the Entra
  External ID OIDC endpoints, **or** `react-native-msal`. Confirm the current
  recommended native-auth + social-IdP package at build time, since this moves.
- **Token storage**: web → in-memory + MSAL silent refresh; mobile →
  `expo-secure-store`.

`IAuthProvider` (in `packages/auth`) abstracts the platform difference:
```ts
interface IAuthProvider {
  signIn(provider: "apple" | "google" | "microsoft" | "facebook"): Promise<void>;
  signOut(): Promise<void>;
  getAccessToken(): Promise<string | null>; // refreshes silently if needed
  currentUser: { id: string; name?: string; provider: string } | null;
}
// impls: WebMsalAuthProvider | NativeMsalAuthProvider | FakeAuthProvider (tests/Storybook)
```

---

## Local dev auth bypass & CIAM quirks

For local development without a live Entra session, the stack supports running
with auth fully bypassed on both sides:

- **Frontend**: leave `NEXT_PUBLIC_ENTRA_CLIENT_ID` unset/empty in `.env.local`.
  `readEntraConfig()` then returns `null`, `Providers.tsx` falls back to
  `FakeAuthProvider` instead of `WebMsalAuthProvider`. `FakeAuthProvider` now
  starts **pre-authenticated** (`currentUser` defaults to a `dev-user` identity)
  so `AuthGuard` doesn't bounce you to `/login` — no sign-in click needed.
- **Backend**: leave `Entra:Authority` / `Entra:ClientId` empty in
  `appsettings.Local.json`. `Program.cs` then registers `LocalAuthHandler`
  instead of JWT Bearer.

`LocalAuthHandler` (`robe.infrastructure/Auth/LocalAuthHandler.cs`) resolves
the current user in priority order so it works whether or not a real token is
attached:
1. **`Authorization: Bearer <jwt>`** — decodes the JWT **payload only** (no
   signature/issuer validation — local dev only) and pulls `oid`/`sub` as the
   user id, plus `name`/`given_name`+`family_name`/`email`/`idp`. This means
   signing in for real (Google via CIAM) on the frontend still gives the
   backend your real identity even though the backend isn't doing crypto
   validation.
2. **`X-User-Id` header** — explicit override for direct API testing (curl,
   Swagger "Authorize" → `X-User-Id`). Not used by `FakeAuthProvider` — it
   sends a fake JWT Bearer token instead (see note below).
3. **No credentials at all** (no Bearer token, no/blank `X-User-Id`) — returns
   `AuthenticateResult.NoResult()`, so `[Authorize]` returns `401`. There is
   **no implicit fallback identity anymore** — a request truly has to send
   something to authenticate.

**JWT shape requirement**: `FakeAuthProvider.getAccessToken()` returns a minimal
fake JWT (`header.payload.sig`) so `LocalAuthHandler.TryBuildTicketFromJwt` can
decode the `sub` claim without signature validation:
```ts
// packages/auth/src/FakeAuthProvider.ts
const payload = btoa(JSON.stringify({ sub: this.currentUser.id, name: "..." }))
  .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
return `eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.${payload}.fakesig`;
```
A plain string like `"fake-access-token"` has no dots, fails the 3-part check,
and causes a 401 on every API call. If you fork `FakeAuthProvider`, always
return a properly shaped `header.payload.sig` fake from `getAccessToken()`.

Testing the backend directly (outside the frontend) hits the same requirement:
hitting `/swagger` locally and clicking "Try it out" with no header also 401s.
Swagger UI exposes an `X-User-Id` "Authorize" option (alongside `Bearer`) for
exactly this — see "API contract & OpenAPI" in `CLAUDE.md`. Set it once to a
dev id like `dev-user` and every Swagger call authenticates the same way
`FakeAuthProvider` does, without needing a real or hand-crafted JWT.

To switch back to real Entra validation: uncomment `Entra:Authority` /
`Entra:ClientId` in `appsettings.Local.json` and set
`NEXT_PUBLIC_ENTRA_CLIENT_ID` (+ related `NEXT_PUBLIC_ENTRA_*` vars) in
`apps/web/.env.local`. Restart both processes — config is read once at
startup (`reloadOnChange: false`).

**CIAM + Google federation gives a sparse ID token.** By default CIAM user
flows don't return `given_name`, `family_name`, `name`, or `email` claims for
social-federated sign-ins, so `WebMsalAuthProvider`'s `accountToUser()` can't
build a real display name from the token alone. Current fallback order:
`given_name`+`family_name` → `name` claim → `account.name` → email
(`email` claim → `preferred_username` claim → `account.username` if it looks
like an email). If everything is empty, `AppShell` displays
**"`{Provider}` Account"** (e.g. "Google Account") instead of the raw OID GUID.
To get the user's actual name/email into the token: in the Entra admin center,
open the CIAM user flow → **User attributes** and **Application claims** →
enable **Display Name** and **Email Address**.

### Automating the redirect URI (`infra/Azure/bicep/entra-redirect-uri.sh`)

Azure Static Web Apps assigns a **new random hostname every time the SWA
resource is deleted and recreated** (e.g. `dev-teardown.sh` → `dev-create.sh`).
Since MSAL requires the exact redirect URI to be pre-registered on the Entra
app registration, every stage rebuild used to require manually adding
`https://<new-random-hostname>/auth/blank` in the Entra admin center before
sign-in would work again.

`entra-redirect-uri.sh` automates this via Microsoft Graph (client-credentials
auth), parsed with plain `grep`/`sed` rather than `jq` (not reliably available
on every dev machine) — `deploy-frontend.sh` registers the URI after every
build, and `dev-teardown.sh` deregisters it before deleting the resource group.
Both are **opt-in and no-op cleanly** (print a message, don't fail the script)
if the three `ENTRA_GRAPH_*` env vars below aren't set — so nothing breaks for
anyone not using real Entra sign-in.

**✅ One-time setup already done** for this project (`sp-robe-entra-redirect-uri-manager`
in the `vestraoauth.onmicrosoft.com` CIAM tenant, owner of the target app,
`Application.ReadWrite.OwnedBy` granted + admin-consented). Documented here so
it doesn't need repeating if the credential is ever rotated or a second app
needs the same automation:

```bash
# This is a *separate* Entra tenant from the one your az session normally
# manages Azure resources in — sign into it explicitly. --use-device-code
# works headlessly (prints a URL + code to complete in any browser).
az login --tenant vestraoauth.onmicrosoft.com --allow-no-subscriptions --use-device-code

# 1. Create a dedicated app registration + service principal for this automation.
APP_ID=$(az ad app create --display-name "sp-robe-entra-redirect-uri-manager" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# 2. Grant it the Microsoft Graph APPLICATION permission Application.ReadWrite.OwnedBy
#    (scoped — only lets it manage apps it owns, not every app in the tenant).
ROLE_ID=$(az ad sp show --id 00000003-0000-0000-c000-000000000000 \
  --query "appRoles[?value=='Application.ReadWrite.OwnedBy'].id" -o tsv)
az ad app permission add --id "$APP_ID" --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions "${ROLE_ID}=Role"
az ad app permission admin-consent --id "$APP_ID"

# 3. Add it as an owner of the target app (d4cd6bf9-...) — required for the
#    OwnedBy-scoped permission to actually apply to it.
AUTOMATION_SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
az ad app owner add --id d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9 --owner-object-id "$AUTOMATION_SP_OBJECT_ID"

# 4. Create a client secret and write it STRAIGHT to the gitignored env file —
# never print it to the terminal or paste it into any tracked file/doc/chat.
# infra/Azure/bicep/.env.entra-graph is listed in the root .gitignore.
CRED_JSON="$(az ad app credential reset --id "$APP_ID" --years 1 -o json)"
{
  echo "export ENTRA_GRAPH_TENANT_ID=\"$(echo "$CRED_JSON" | grep -o '"tenant"[^,}]*' | sed -E 's/.*"([^"]*)"$/\1/')\""
  echo "export ENTRA_GRAPH_CLIENT_ID=\"$(echo "$CRED_JSON" | grep -o '"appId"[^,}]*' | sed -E 's/.*"([^"]*)"$/\1/')\""
  echo "export ENTRA_GRAPH_CLIENT_SECRET=\"$(echo "$CRED_JSON" | grep -o '"password"[^,}]*' | sed -E 's/.*"([^"]*)"$/\1/')\""
} > infra/Azure/bicep/.env.entra-graph
unset CRED_JSON
```

Then `source infra/Azure/bicep/.env.entra-graph` before running `dev-create.sh` /
`dev-teardown.sh` / `deploy-frontend.sh`. **Never** put the actual tenant/client
ID/secret values inline in `scratch-commands.txt`, this file, or any other
tracked file — `.env.entra-graph` is the only place they should live, and it's
gitignored specifically so that's structurally hard to get wrong.

---

## Design tokens (from the approved mock)

Editorial / warm-neutral direction. Single source of truth in `packages/tokens`.

```ts
export const tokens = {
  color: {
    bg:       "#ECE4D6",  // warm paper
    bg2:      "#E2D7C4",
    surface:  "#FBF8F1",  // cards
    surface2: "#F2ECDF",
    ink:      "#221C15",  // primary text
    ink2:     "#5A5043",
    muted:    "#988C78",
    line:     "#D8CDBA",
    accent:   "#9C4A2E",  // terracotta — CTAs, scores
    accent2:  "#C2774E",
    gold:     "#A98C4B",
  },
  font: {
    display: "Fraunces",        // serif, headings — opsz/ital available
    body:    "Hanken Grotesk",  // sans, UI text
  },
  radius: { sm: 9, md: 14, lg: 18, pill: 100 },
  space:  { xs: 6, sm: 10, md: 14, lg: 22, xl: 32 },
};
```

Fonts: web via `@fontsource/fraunces` + `@fontsource/hanken-grotesk` (or Google
Fonts); mobile via `@expo-google-fonts/fraunces` +
`@expo-google-fonts/hanken-grotesk`.

Core components (built per platform, tokens-driven): `Button` (fill/ghost),
`Chip` (toggle), `GarmentCard`, `TraitRow`, `ProviderButton`, `ScoreBadge`,
`PaletteStrip`, `FormalityDots`, `ConfidenceBar`, `AddTile`.

---

## API layer

Typed client + TanStack Query hooks, behind an interface so it's mockable.

```ts
interface IApiClient {
  getMe(): Promise<MeUser>;                                      // auth check
  analyzeGarment(image: ImageInput): Promise<GarmentTraits>;     // API #1 single
  analyzeBatch(images: BatchImageInput[]): Promise<BatchAnalyzeResult>; // API #1 batch
  addGarment(input: AddGarmentInput): Promise<Garment>;          // API #2
  listGarments(q?: GarmentQuery): Promise<Garment[]>;            // API #2
  getGarment(id: string): Promise<Garment>;                      // API #2
  deleteGarment(id: string): Promise<void>;                      // API #2
  generateProfile(): Promise<StyleProfile>;                      // API #3
  getProfile(): Promise<StyleProfile | null>;                    // API #3
  getRecommendations(ctx: RecommendationContext): Promise<Recommendation[]>; // API #4
}
// impls: HttpApiClient (real) | FakeApiClient (seeded fixtures, for tests/Storybook/offline)
```

- An auth interceptor calls `auth.getAccessToken()` and attaches the Bearer
  header; on `401` it triggers silent refresh, then redirects to sign-in.
- Every backend response carries an **`X-Correlation-Id`** header (echoed from
  the request if the client sent one, otherwise server-generated) tying that
  request to its backend logs/metrics/alerts. Useful in bug reports: log it
  alongside client-side errors so a "this broke" report can be traced
  server-side without timestamp-guessing. Not required — if the client never
  sends one, the backend mints its own — but `HttpApiClient` may optionally
  generate and attach one per request later for true end-to-end tracing.
- Expose **TanStack Query** hooks so screens never call the client directly:
  `useGarments`, `useGarment`, `useAddGarment`, `useDeleteGarment`,
  `useAnalyzeGarment`, `useAnalyzeBatch`, `useStyleProfile`, `useGenerateProfile`,
  `useRecommendations`. Server state lives in Query; keep client-only state
  (filters, form drafts) minimal and local.
- `useRecommendations(ctx, enabled?)` accepts an optional `enabled` flag so the
  shop page can skip fetching when no profile exists yet.
- `GET /api/garments` returns `{ items, page, pageSize }`, not a bare array —
  `listGarments()` unwraps `res.items` so `IApiClient.listGarments()` keeps
  returning `Garment[]` to callers. Don't change the client's return type
  without also reconciling this unwrap.
- `types` package mirrors the backend DTOs exactly — `GarmentTraits`, `Garment`,
  `StyleProfile`, `InventoryItem`, `RecommendationContext`, `Recommendation`,
  `MeUser`, plus batch-extraction types for `POST /api/garments/analyze-batch`:
  `BatchImageInput`, `ClothingItemTraitsResult` (richer per-item schema),
  `PersonTraitsResult` (person grouping with `clothingItems[]`),
  `ImageTraitsResult` (per-image result with `people[]` and `warnings[]`),
  `BatchAnalyzeResult` (`images[]` + `modelVersion`). `ClothingItemTraitsResult`
  uses `category`/`type`/`subtype` (not `GarmentTraits` directly) — `UploadFlow`
  maps it down via `mapToGarmentTraits()` before calling `addGarment`. Field names
  match the backend's camelCase JSON serialization:
  - `Garment` includes `modifiedAt`, `createdByUserId`, `modifiedByUserId`
    (matching `GarmentResponse`).
  - `StyleProfile` uses `createdAt`/`modifiedAt` (not `generatedAt`) to match
    `ProfileResponse`.
  - `InventoryItem` has flat `price`/`currency` fields, `vendorId`, `url`,
    `name`, `description`, `inStock` (matching the backend entity).
  - `Recommendation` uses `inventoryItem` and `reasoning` (matching backend
    property names).
  - `RecommendationContext` uses `maxBudget`, `currency`, `categories`,
    `occasion`, `count` (matching `RecommendRequest`).
  **Do not hand-write these**: codegen from the backend's committed
  `contracts/openapi.json` (see "API contract & OpenAPI" in CLAUDE.md) using
  `openapi-typescript` for types and `orval` for the client/hooks, wired as a
  `pnpm gen:api` script. Re-run it whenever the backend bumps the spec.

---

## Screens (map to mock + backend)

| Screen | Route | Backend | Key components | Status |
|---|---|---|---|---|
| Sign in | `/login` | Entra | ProviderButton ×4 | ✅ |
| Wardrobe | `/wardrobe` | #1–2 | FilterChips, GarmentCard grid, AddTile, UploadFlow modal | ✅ |
| Garment detail | `/wardrobe/[id]` | #1–2 | hero image, TraitRow, FormalityDots, ConfidenceBar | ✅ |
| Style profile | `/profile` | #3 | style Chips, PaletteStrip, FormalityDots, summary quote | ✅ |
| Recommendations | `/shop` | #4 | FilterChips, budget input, recommendation cards, ScoreBadge | ✅ |

Navigation: web = left sidebar (Wardrobe / Style / Shop) + account footer;
mobile = bottom tab bar (Closet / Style / Shop). Both gate everything except
`/login` behind a valid session.

### Implemented component inventory (web)

| Component | File | Purpose |
|---|---|---|
| `Providers` | `components/Providers.tsx` | QueryClient + auth + API client provider wiring |
| `AuthGuard` | `components/AuthGuard.tsx` | Route gate; redirects unauthenticated to `/login` |
| `AppShell` | `components/AppShell.tsx` | Left sidebar nav + account footer + sign-out |
| `UploadFlow` | `components/UploadFlow.tsx` | Multi-step modal supporting single and batch uploads. Single file: pick → analyze → review → save. Batch (2+ files): pick → analyze all → batch-review (per-image/per-person checkboxes) → save selected. Uses `useAnalyzeGarment` for single, `useAnalyzeBatch` for batch. Maps `ClothingItemTraitsResult` → `GarmentTraits` via `mapToGarmentTraits()`. |
| `GarmentCard` | `components/GarmentCard.tsx` | Grid tile with garment image + primary color badge |
| `FilterChips` | `components/FilterChips.tsx` | Category filter toggle chips |
| `AddTile` | `components/AddTile.tsx` | "+" button tile to trigger upload |
| `TraitRow` | `components/TraitRow.tsx` | Label–value row for trait details |
| `FormalityDots` | `components/FormalityDots.tsx` | Visual 1–5 dot indicator |
| `ConfidenceBar` | `components/ConfidenceBar.tsx` | Progress bar for extraction confidence |
| `PaletteStrip` | `components/PaletteStrip.tsx` | Color swatches with weight labels |
| `ProviderButton` | `components/ProviderButton.tsx` | Social sign-in button |
| `ScoreBadge` | `components/ScoreBadge.tsx` | Accent pill showing fit score as percentage |

---

## Deployment (local dev + Azure SWA per stage)

### Local dev

Copy `.env.local.example` to `vestra/apps/web/.env.local` before first run.
The example ships with **Mode 1** (fakes) as the default:

```
# Mode 1 (default) — local fakes, no Entra tenant needed
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
# leave NEXT_PUBLIC_ENTRA_CLIENT_ID unset → FakeAuthProvider
```

Mode 2 (real Entra against local API): uncomment the four `NEXT_PUBLIC_ENTRA_*`
vars. The backend must also have `Entra:Authority` + `Entra:ClientId` set in
`appsettings.Local.json`. See "Local dev auth bypass" above.

```bash
# Run backend in local fakes mode (no Azure services):
ASPNETCORE_ENVIRONMENT=Local dotnet run --project robe.api

# Run frontend:
cd vestra && pnpm dev
```

### Azure SWA — build config requirements

The app deploys as a **static export** — no Node SSR runtime on SWA. This was
a deliberate switch away from `output: "standalone"` (the original plan) to
sidestep SWA's Node 18-vs-20 runtime mismatches and cold-start warmup timeouts;
none of the app's routes need server rendering (auth, data fetching, and
routing are all client-side via TanStack Query + MSAL).

- **`next.config.mjs`** — `output: "export"` (produces static HTML/CSS/JS in
  `out/`) with `images: { unoptimized: true }` (required alongside
  `output: "export"` — the Image Optimization API needs a server, which static
  export doesn't have; components use plain `<img>` anyway, so this has no
  practical effect).
- **`public/staticwebapp.config.json`** — `navigationFallback.rewrite` to
  `/index.html` (excluding `/_next/*` and `/favicon.ico`) so client-side routes
  (`/wardrobe/[id]`, etc.) resolve correctly on refresh/deep-link instead of
  404ing. Lives under `public/` so `next build` copies it into `out/`
  automatically — no separate copy step needed.
- Infra still provisions **Standard SKU** (see `stage.bicep`) — that requirement
  was for the SSR path this app no longer uses; whether Free would suffice for
  pure static hosting hasn't been evaluated, so no change proposed here.

No manual asset-copying step is needed — `next build` with `output: "export"`
puts everything `out/` needs in one place.

### `NEXT_PUBLIC_*` vars are baked at build time

`NEXT_PUBLIC_*` variables are embedded into the JS bundle during `next build`.
Azure SWA appsettings are runtime-only for SSR — they will **not** affect the
client-side bundle. Always inject them as shell env vars before building:

```bash
NEXT_PUBLIC_API_BASE_URL=https://app-robe-gamma.azurewebsites.net \
NEXT_PUBLIC_ENTRA_CLIENT_ID=d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9 \
...
  pnpm --filter @vestra/web run build
```

### `deploy-frontend.sh` — multi-stage deploy script

`infra/Azure/bicep/deploy-frontend.sh` handles build + SWA deploy for any stage.
Prereq: the stage's Bicep infra (SWA resource) must already exist.

```bash
cd infra/Azure/bicep

# Dev frontend (uses FakeAuthProvider — no Entra env vars needed):
./deploy-frontend.sh --stage=dev --yes

# Gamma/live frontend (with real Entra):
export FRONTEND_ENTRA_AUTHORITY="https://vestraoauth.ciamlogin.com/vestraoauth.onmicrosoft.com"
export FRONTEND_ENTRA_CLIENT_ID="d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9"
export FRONTEND_ENTRA_API_SCOPE="api://d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9/access_as_user"
./deploy-frontend.sh --stage=gamma --yes
./deploy-frontend.sh --stage=live  --yes
```

The script: reads the SWA hostname via `az staticwebapp show`, runs
`pnpm install --frozen-lockfile` + `pnpm --filter @vestra/web run build` with
the correct env vars, retrieves the SWA deployment token, and runs
`@azure/static-web-apps-cli deploy --app-location out/` — pointing straight at
the static export output, no asset-copying step needed.

API endpoints baked in per stage:
| Stage | API | SWA URL |
|---|---|---|
| dev | `https://app-robe-dev.azurewebsites.net` | `https://swa-robe-dev.<hash>.azurestaticapps.net` |
| gamma | `https://app-robe-gamma.azurewebsites.net` | `https://swa-robe-gamma.<hash>.azurestaticapps.net` |
| live | `https://app-robe-live.azurewebsites.net` | `https://swa-robe-live.<hash>.azurestaticapps.net` |

For the full dev stage end-to-end (infra + backend + frontend in one command),
use `dev-create.sh` instead — it calls `deploy-frontend.sh` internally. See
`scratch-commands.txt` for all deploy commands with flags.

---

## Kickoff prompt (paste into Claude Code, in the client repo)

> Read FRONTEND.md (and CLAUDE.md for the API contracts). We're doing **step 0
> only**: scaffold the pnpm + Turborepo monorepo with apps/web (Next.js App
> Router), apps/mobile (Expo Router), and the packages/ (tokens, types, api,
> auth) shells. Wire the design tokens and the shared TS types from CLAUDE.md.
> No screens yet. Plan the structure first, wait for my OK, then scaffold.

## Per-feature prompt (reuse)

> Step N−1 is approved. Now build **step N: \<feature\>** per FRONTEND.md. Plan
> first — component tree, the TanStack Query hooks it uses, and tests against
> FakeApiClient / FakeAuthProvider. Build it on both web and mobile sharing the
> packages. Wait for my OK, implement, show it working, then stop.