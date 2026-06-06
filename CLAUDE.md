# Project: Wardrobe Style Recommendation Engine

A lifestyle app: users upload photos of their wardrobe, the system extracts
structured traits per garment, builds a style profile, and recommends what to
wear or buy — eventually matching against multi-vendor inventory.

---

## Build order (one API at a time)

1. **Garment trait extraction** — image in → structured traits out (no storage)  ✅ DONE
2. **Storage + auth** — persist garments/images, map to authenticated user  ✅ DONE
3. **Style profile generation** — analyze a user's garments → style profile  ✅ DONE
4. Recommendation matching — profile + inventory → ranked matches  ✅ DONE
5. Event-based suggestions — profile + event context → outfits / buy list  ⬜ TODO
6. Vendor inventory ingestion — 3rd-party APIs → per-merchant containers  ⬜ TODO
7. Vector search + filtering — semantic pre-filter before LLM ranking  ⬜ TODO

APIs 1–4 are detailed below and built. APIs 5–7 have design notes (see "Planned
APIs" near the end) but no full contracts yet — flesh out one before building it.
When you finish an API, change its ⬜ to ✅ here so this stays the source of truth
for status.

---

## Stack  (← edit to match your real choices)

- Language / framework: .NET 8 / ASP.NET Core Web API
- Cloud: Azure
- DB: Azure SQL (relational) or Cosmos DB (NoSQL) + Azure Blob Storage for images
- Auth: Microsoft Entra ID — use Entra External ID (formerly Azure AD B2C) for consumer sign-in; JWT bearer tokens
- LLM / Vision: Azure OpenAI Service, vision-capable model (gpt-4o) via the Chat Completions API
- Hosting: Azure App Service or Azure Functions
- Tests: xUnit + WebApplicationFactory for integration tests

---

## Working rules (read every session)

- Build **ONE** API at a time. Do **not** start the next until I approve.
- **Plan first**: before writing code, show the endpoint contract and your test
  approach, then wait for my "go".
- Always write tests and show them **passing** before you stop.
- Do not modify earlier, already-approved APIs unless I explicitly ask.
- Keep each endpoint independently runnable and testable.
- **Interface-first**: every external dependency (LLM/vision, DB, blob storage,
  auth) MUST sit behind an interface. Controllers/handlers depend ONLY on
  interfaces — never on a concrete class or SDK type. Register the concrete
  implementation in `Program.cs` so swapping it is a one-line change. See the
  "Dependency model" section for the required interfaces per API.
- Treat the `GarmentTraits` schema below as the source of truth. If you think it
  needs to change, propose the change and wait — do not silently diverge.
- **Keep the OpenAPI spec current**: regenerate `contracts/openapi.json` whenever
  an endpoint or DTO changes (see "API contract & OpenAPI"); the frontend codegens
  from it.

---

## Dependency model (interface-first)

Each API is built against abstractions. The concrete implementation is chosen at
startup via DI, so you can swap providers (or inject a fake in tests) without
touching the controller or business logic.

Swapping is one line in `Program.cs`:
```csharp
builder.Services.AddScoped<ITraitsExtractor, AzureOpenAITraitsExtractor>();
// later, to switch providers — change only this line:
// builder.Services.AddScoped<ITraitsExtractor, ClaudeTraitsExtractor>();
```

Tests register the fake instead:
```csharp
services.AddScoped<ITraitsExtractor, FakeTraitsExtractor>();
```

### Interfaces by API

```csharp
// ---- API #1: extraction ----
public interface ITraitsExtractor
{
    Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default);
}
// impls: AzureOpenAITraitsExtractor | ClaudeTraitsExtractor | FakeTraitsExtractor

// ---- API #2: storage + auth ----
public interface IGarmentRepository
{
    Task<Garment> AddAsync(Garment garment, CancellationToken ct = default);
    Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default);
}
// impls: SqlGarmentRepository | CosmosGarmentRepository | InMemoryGarmentRepository

public interface IImageStore
{
    Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default); // returns URL
    Task DeleteAsync(string key, CancellationToken ct = default);
}
// impls: AzureBlobImageStore | S3ImageStore | InMemoryImageStore

public interface ICurrentUser
{
    string UserId { get; }   // resolved from the validated JWT, not the request body
}
// impls: HttpContextCurrentUser | FakeCurrentUser

// ---- API #3: profile generation ----
public interface IProfileGenerator
{
    Task<StyleProfile> GenerateAsync(IReadOnlyCollection<GarmentTraits> traits, CancellationToken ct = default);
}
// impls: AzureOpenAIProfileGenerator | ClaudeProfileGenerator | FakeProfileGenerator

public interface IProfileRepository
{
    Task SaveAsync(string userId, StyleProfile profile, CancellationToken ct = default);
    Task<StyleProfile?> GetAsync(string userId, CancellationToken ct = default);
}
// impls: SqlProfileRepository | CosmosProfileRepository | InMemoryProfileRepository

// ---- API #4: recommendation matching ----
public interface IInventoryRepository
{
    // candidate pool to match against; vendor-backed impls arrive in API #6
    Task<IReadOnlyList<InventoryItem>> QueryAsync(InventoryQuery query, CancellationToken ct = default);
}
// impls: InMemoryInventoryRepository (seeded) | <vendor-backed, API #6>

public interface IRecommender
{
    Task<IReadOnlyList<Recommendation>> RankAsync(
        StyleProfile profile,
        IReadOnlyCollection<InventoryItem> candidates,
        RecommendationContext context,
        CancellationToken ct = default);
}
// impls: AzureOpenAIRecommender | ClaudeRecommender | FakeRecommender
```

Project layout: interfaces and domain types (`GarmentTraits`, `Garment`,
`StyleProfile`, `ImageInput`) live in a **Core/Domain** project with **no** SDK
references. Each concrete implementation lives in an **Infrastructure**
project/folder and is the only place an SDK (Azure OpenAI client, EF Core, Blob
SDK) is referenced. Controllers reference Core only.

---

## API contract & OpenAPI (single source of truth for the frontend)

The backend is the contract owner. It emits an **OpenAPI document** that the
frontend (`packages/types` and the API client in `FRONTEND.md`) generates from,
so the DTOs are never hand-copied in two repos and can't silently drift.

Backend:
- Generate the spec from the running API. On **.NET 8** use **Swashbuckle**
  (`Swashbuckle.AspNetCore`); on **.NET 9+** use the built-in
  **`Microsoft.AspNetCore.OpenApi`** package. (Confirm which ships with your
  template — this changed between versions.)
- Expose it at `/openapi/v1.json` (or `/swagger/v1/swagger.json`) and also
  **write it to a file** committed to the repo (e.g. `contracts/openapi.json`)
  via a build step, so codegen doesn't require a running server.
- Annotate DTOs and endpoints so the spec is accurate: explicit request/response
  types, status codes (`[ProducesResponseType]` / typed results), required vs
  nullable fields. The generated spec is only as good as these annotations.
- **Working rule:** when an endpoint or DTO changes, regenerate
  `contracts/openapi.json` in the same change — treat a stale spec as a bug.

Frontend codegen (from `contracts/openapi.json`):
- Types: **`openapi-typescript`** → feeds `packages/types`.
- Client + TanStack Query hooks: **`orval`** (or `openapi-typescript-codegen`).
- Microsoft-stack alternative: **Kiota** generates a typed client (incl. TS) and
  fits if you prefer a Microsoft-maintained toolchain.
- Wire codegen as an npm script (`pnpm gen:api`) so a contract bump is a
  one-command refresh on the client side.

Net effect: `GarmentTraits`, `Garment`, `StyleProfile`, `InventoryItem`,
`RecommendationContext`, and `Recommendation` are authored once here and flow to
the clients automatically.

---

## Core data model: `GarmentTraits`

This is the contract everything downstream keys off (profile, matching,
recommendations). Get it right before building anything past API #1.

```jsonc
{
  "category":        "top",            // enum: top | bottom | dress | outerwear | footwear | accessory | other
  "subcategory":     "t-shirt",        // free-ish string: t-shirt, blouse, jeans, chinos, sneakers, ...
  "primaryColor":    { "name": "navy", "hex": "#1f2a44" },
  "secondaryColors": [ { "name": "white", "hex": "#ffffff" } ],
  "pattern":         "solid",          // enum: solid | striped | plaid | checked | floral | graphic | other
  "material":        "cotton",         // inferred; may be null when uncertain
  "fit":             "regular",        // enum: slim | regular | loose | oversized | null
  "formality":       2,                // int 1–5: 1 very casual ... 5 formal
  "seasonality":     ["spring","summer"], // any of: spring | summer | fall | winter | all
  "styleTags":       ["minimalist","casual"], // free tags from a controlled vocab (see below)
  "occasions":       ["everyday","weekend"],  // everyday | work | formal | sport | event | ...
  "confidence":      0.86              // overall extraction confidence 0–1
}
```

Suggested controlled style vocabulary (extend as needed):
`minimalist, classic, casual, streetwear, formal, bohemian, sporty, vintage,
preppy, edgy, romantic`

Per-field confidence is optional but recommended for color/material/style, since
vision extraction is probabilistic and you'll want to flag low-confidence fields
for user correction.

---

## API #1 — Garment trait extraction

Stateless. Takes an image, returns traits. No DB, no auth required to build/test
it in isolation (you can add auth when it's wired into the app).

**Depends on (inject):** `ITraitsExtractor`

**`POST /api/garments/analyze`**

Request (multipart/form-data **or** JSON with base64):
```jsonc
{ "imageBase64": "<...>", "mimeType": "image/jpeg" }
```

Response `200`:
```jsonc
{ "traits": { /* GarmentTraits object */ }, "modelVersion": "azure-openai-gpt-4o" }
```

Errors: `400` (bad/missing image), `422` (no garment detected), `502` (model
call failed).

Implementation notes: send the image to the Azure OpenAI vision model (gpt-4o)
via the Chat Completions API, using **JSON mode / structured outputs** with a
prompt that demands JSON matching the GarmentTraits schema and nothing else;
still parse defensively (strip any code fences, validate against the schema,
reject/repair on parse failure).

Tests to show passing:
- valid image → well-formed `GarmentTraits` with required fields populated
- non-clothing image → `422`
- missing/oversized image → `400`
- malformed model output → handled gracefully, no 500 leak

---

## API #2 — Storage + auth

Persists garments for the logged-in user. Image bytes go to Azure Blob Storage;
the record (traits + image URL + userId) goes to the DB.

All endpoints require a valid Entra (External ID / B2C) JWT; `userId` is taken
from the token, not the request body.

**Depends on (inject):** `IGarmentRepository`, `IImageStore`, `ICurrentUser`

**`POST /api/garments`** — store a garment
```jsonc
// request
{ "traits": { /* GarmentTraits */ }, "imageBase64": "<...>", "mimeType": "image/jpeg" }
// response 201
{ "id": "grm_123", "userId": "usr_456", "traits": { ... }, "imageUrl": "https://.../grm_123.jpg", "createdAt": "..." }
```

**`GET /api/garments`** — list current user's garments (support `?category=` filter, paging)
**`GET /api/garments/{id}`** — single garment (404 if not owned by caller)
**`DELETE /api/garments/{id}`** — remove garment + its blob

Tests to show passing:
- store → retrieve round-trips traits intact
- user A cannot read or delete user B's garment (returns 404, not 403, to avoid leaking existence)
- request with no/invalid token → `401`
- list returns only the caller's items; category filter works
- delete removes both DB row and Blob Storage object

Note: a clean pattern is to have the client call API #1 (`/analyze`), let the
user confirm/correct traits in the UI, then call API #2 (`/garments`) to save —
keeping extraction and persistence decoupled.

---

## Auth verification endpoint — `GET /api/me`

A deliberately trivial, protected endpoint whose only job is to confirm a token
is valid and reaches the backend. The frontend pings it during auth bring-up
(FRONTEND.md step 1b) as the very first "the token works" check, before any real
feature calls. No DB, no business logic.

Requires a valid Entra (External ID / B2C) JWT.

**Depends on (inject):** `ICurrentUser`

**`GET /api/me`**
```jsonc
// response 200 — echoes identity derived from the validated token, not the request
{ "userId": "usr_456", "name": "Sundeep A.", "provider": "apple" }
```
Pull `userId` from `ICurrentUser` (the token's subject claim); `name`/`provider`
come from token claims if present. No request body, no params.

Tests to show passing:
- valid token → `200` with the caller's `userId`
- missing/invalid/expired token → `401`
- the `userId` matches the token subject (never anything client-supplied)

---

## API #3 — Style profile generation

Reads all of a user's stored garments, sends the aggregated traits to the model,
returns a structured style profile. Cache/store the result; regenerate on demand
or when the wardrobe changes meaningfully.

**Depends on (inject):** `IProfileGenerator`, `IProfileRepository`, `IGarmentRepository`, `ICurrentUser`

**`POST /api/users/me/profile/generate`** — build/refresh profile
**`GET  /api/users/me/profile`** — fetch last generated profile

Profile response `200`:
```jsonc
{
  "dominantStyles":    ["minimalist", "classic"],     // ranked
  "colorPalette":      [ { "name": "navy", "hex": "#1f2a44", "weight": 0.4 }, ... ],
  "formalityRange":    { "min": 1, "max": 3, "typical": 2 },
  "preferredFits":     ["regular", "slim"],
  "seasonalSkew":      { "summer": 0.5, "winter": 0.2, ... },
  "summary":           "Leans minimalist and neutral, favors regular-fit everyday pieces...",
  "garmentCount":      42,
  "generatedAt":       "..."
}
```

Implementation notes: pull traits for the user, build a compact summary (counts
by category/color/style — don't dump 42 raw objects if you can aggregate), send
to Azure OpenAI asking for the profile JSON above. Same defensive-parse
discipline as API #1.

Tests to show passing:
- user with garments → profile reflects their actual trait distribution
- user with zero garments → empty/“insufficient data” profile, not a 500
- `GET` before any `generate` → `404` or empty state (decide and test it)
- profile is scoped to the calling user only

---

## API #4 — Recommendation matching

Takes the user's stored style profile plus a candidate pool of purchasable
inventory, and returns ranked matches the user is likely to buy. The inventory
pool is read through `IInventoryRepository` — for now back it with
`InMemoryInventoryRepository` seeded with sample items, so this API is fully
buildable and testable **before** the real vendor ingestion (API #6) exists.

Requires a valid JWT; the profile and results are scoped to the caller.

**Depends on (inject):** `IRecommender`, `IInventoryRepository`, `IProfileRepository`, `ICurrentUser`

New domain types:
```jsonc
// InventoryItem — a purchasable garment in the candidate pool
{
  "id":        "inv_789",
  "merchant":  "nordstrom",
  "traits":    { /* GarmentTraits */ },     // reuse the same schema
  "price":     { "amount": 49.0, "currency": "USD" },
  "productUrl":"https://...",
  "imageUrl":  "https://..."
}

// RecommendationContext — request-time inputs that shape ranking
{
  "budgetMax":   100.0,                       // optional
  "categories":  ["top","outerwear"],         // optional filter
  "excludeOwned": true                         // optional: skip items like ones they already have
}

// Recommendation — one ranked result
{
  "item":   { /* InventoryItem */ },
  "score":  0.91,                              // 0–1 fit score
  "reason": "Matches your minimalist, neutral palette; regular fit."
}
```

**`POST /api/users/me/recommendations`**
```jsonc
// request  (body is a RecommendationContext; all fields optional)
{ "budgetMax": 100.0, "categories": ["top"], "excludeOwned": true }
// response 200
{ "recommendations": [ { /* Recommendation */ }, ... ] }   // ranked, highest score first
```

Flow: load the caller's `StyleProfile` (via `IProfileRepository`) → query the
candidate pool with the context filters (via `IInventoryRepository`) →
pass profile + candidates + context to `IRecommender.RankAsync`. The ranker
applies hard filters (budget, category) before scoring, then asks the model to
score/justify fit. Keep the candidate set bounded — pre-filter before the model
call (this is exactly the pre-filter that API #7's vector search will optimize
later; for now a simple repository filter is fine).

Tests to show passing (use `FakeRecommender`, `InMemoryInventoryRepository`, in-memory profile repo):
- profile + seeded inventory → non-empty, score-ordered recommendations
- `budgetMax` excludes over-budget items; `categories` filters correctly
- user with no profile yet → `409` with a "generate a profile first" message
- empty candidate pool → empty list, not a 500
- results scoped to the calling user only

---

## Planned APIs (#5–#7) — design notes (not yet specced in full)

Captured so context isn't lost. Before building one, expand it into a full
section (contract + interfaces + tests) matching APIs 1–4, then flip its ⬜ to ✅.
All follow the same interface-first rules and reuse existing domain types.

### #5 — Event-based suggestions
Same shape as API #4, with an added event context input. Caller describes an
occasion ("outdoor summer wedding", "business conference"); system returns
outfit suggestions from owned garments and/or things to buy from inventory.
- Likely new input: `EventContext { description, formalityTarget, season, date? }`.
- Reuses `StyleProfile`, the user's owned `Garment`s (`IGarmentRepository`), and
  `IInventoryRepository`.
- New interface likely `IOutfitSuggester` (impls: AzureOpenAI… | Claude… | Fake…),
  returning suggested outfits (groupings of owned items) plus optional buy list.
- Endpoint sketch: `POST /api/users/me/suggestions` with an `EventContext` body.

### #6 — Vendor inventory ingestion
The heavy lift — pulls inventory from third-party vendors (Amazon, Nordstrom,
Macy's, etc.) into per-merchant containers, normalizes each item into
`InventoryItem` (extracting `GarmentTraits` so it matches owned-garment data),
and keeps it in sync. This is what finally backs `IInventoryRepository` with
real data instead of the in-memory seed.
- One `IVendorConnector` per merchant behind a common interface; a sync
  job/`IInventorySyncService` writes normalized items to storage.
- Concerns to plan for: auth per vendor, rate limits, scheduled refresh,
  trait normalization across differing vendor schemas, dedupe.
- Note: real vendor API access often needs affiliate/partner approval — confirm
  availability before committing to a specific vendor.

### #7 — Vector search + filtering
Optimizes the candidate pre-filter that APIs #4/#5 currently do with simple
repository filters. Embed the user's style profile and all inventory item traits
into vectors, store them, and use semantic search to pull the top-N closest
items before the LLM ranks them — so the model never sees thousands of items.
- On Azure: **Azure AI Search** has built-in vector search (alternatives:
  Pinecone, Weaviate, pgvector on Azure SQL/Postgres).
- New interface likely `IVectorIndex` (upsert vectors, query top-N by similarity)
  + an embedding step behind `IEmbedder`.
- Slots in behind `IInventoryRepository.QueryAsync` (or as the thing that feeds
  candidates to `IRecommender`) — controllers/rankers shouldn't need changes.

---

## Kickoff prompt (paste into Claude Code)

> Read CLAUDE.md. We're building **API #1 only**. Before writing code, give me
> the plan: confirm the endpoint contract, the GarmentTraits parsing strategy,
> and the test list. Wait for my approval, then implement with tests. Show tests
> green, then stop — do not touch any other API.

## Per-API prompt (reuse for #2, #3, ...)

> API #N−1 is approved and tests pass. Now build **API #N: \<name\>** per
> CLAUDE.md. Plan first (contract + test approach), wait for my OK, implement,
> show tests green, then stop. Don't modify earlier APIs.