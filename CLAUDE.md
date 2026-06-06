# Project: Wardrobe Style Recommendation Engine

A lifestyle app: users upload photos of their wardrobe, the system extracts
structured traits per garment, builds a style profile, and recommends what to
wear or buy — eventually matching against multi-vendor inventory.

---

## Build order (one API at a time)

1. **Garment trait extraction** — image in → structured traits out (no storage)
2. **Storage + auth** — persist garments/images, map to authenticated user
3. **Style profile generation** — analyze a user's garments → style profile
4. Recommendation matching — profile + inventory → ranked matches
5. Event-based suggestions — profile + event context → outfits / buy list
6. Vendor inventory ingestion — 3rd-party APIs → per-merchant containers
7. Vector search + filtering — semantic pre-filter before LLM ranking

This file defines APIs 1–3 in detail. Do not build 4+ until those are done.

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
```

Project layout: interfaces and domain types (`GarmentTraits`, `Garment`,
`StyleProfile`, `ImageInput`) live in a **Core/Domain** project with **no** SDK
references. Each concrete implementation lives in an **Infrastructure**
project/folder and is the only place an SDK (Azure OpenAI client, EF Core, Blob
SDK) is referenced. Controllers reference Core only.

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

## Kickoff prompt (paste into Claude Code)

> Read CLAUDE.md. We're building **API #1 only**. Before writing code, give me
> the plan: confirm the endpoint contract, the GarmentTraits parsing strategy,
> and the test list. Wait for my approval, then implement with tests. Show tests
> green, then stop — do not touch any other API.

## Per-API prompt (reuse for #2, #3, ...)

> API #N−1 is approved and tests pass. Now build **API #N: \<name\>** per
> CLAUDE.md. Plan first (contract + test approach), wait for my OK, implement,
> show tests green, then stop. Don't modify earlier APIs.
