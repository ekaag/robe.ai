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
  analyzeGarment(image: ImageInput): Promise<GarmentTraits>;     // API #1
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
- Expose **TanStack Query** hooks so screens never call the client directly:
  `useGarments`, `useGarment`, `useAddGarment`, `useDeleteGarment`,
  `useAnalyzeGarment`, `useStyleProfile`, `useGenerateProfile`,
  `useRecommendations`. Server state lives in Query; keep client-only state
  (filters, form drafts) minimal and local.
- `useRecommendations(ctx, enabled?)` accepts an optional `enabled` flag so the
  shop page can skip fetching when no profile exists yet.
- `types` package mirrors the backend DTOs exactly — `GarmentTraits`, `Garment`,
  `StyleProfile`, `InventoryItem`, `RecommendationContext`, `Recommendation`,
  `MeUser`. Field names match the backend's camelCase JSON serialization:
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
| `UploadFlow` | `components/UploadFlow.tsx` | Multi-step modal: pick image → analyze → review → save |
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