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
- Cloud: Azure today; every external dependency sits behind an interface so a
  second provider (AWS/GCP) can be added later without touching callers — see
  "Multi-cloud folder convention" in "Secrets & per-stage cloud config" below
- DB: Cosmos DB (NoSQL), SQL API — `garments` container partitioned on `/userId`,
  serverless capacity mode in every stage (see `stage.bicep`) + Azure Blob Storage
  for images (`garment-images` container, blob-level public read)
- Auth: Microsoft Entra ID — use Entra External ID (formerly Azure AD B2C) for consumer sign-in; JWT bearer tokens
- LLM / Vision: Azure OpenAI Service, vision-capable model via the Chat Completions API.
  Current deployment: **gpt-5-mini/2025-08-07** (GlobalStandard, eastus) — vision-capable,
  cost-efficient, strong instruction following for structured JSON extraction. All gpt-4o and
  gpt-4.1 versions were deprecated for new deployments as of July 2026 (gpt-4.1/2025-04-14
  entered deprecating state July 2026). Bump `visionModelName`/`visionModelVersion` in
  `infra/Azure/bicep/modules/stage.bicep` to upgrade all stages at once.
- Secrets: Azure Key Vault, one per deployment stage — see "Secrets & per-stage
  cloud config" below
- Hosting: Azure App Service (Linux), one per stage (dev/gamma/live) — see
  "Secrets & per-stage cloud config" below
- Observability: Application Insights / Azure Monitor (logs, metrics, alerts); Local
  console + in-memory implementation for dev/tests — see "Observability" below
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
- **Multi-cloud folder convention**: a cloud-specific implementation lives in a
  per-feature `Azure/` subfolder with an `Azure`-prefixed class name (e.g.
  `Persistence/Azure/AzureCosmosGarmentRepository.cs`). Provider-agnostic
  implementations (`Fake*`, `InMemory*`, `MockSecretManager`) stay directly
  under the feature folder, not in `Azure/`. A future AWS/GCP implementation
  gets its own sibling subfolder + provider prefix — never rename or move the
  existing Azure implementation to make room for it. See "Secrets & per-stage
  cloud config" below.

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

// ---- API #1 (extension): multi-image, multi-person fashion extraction ----
public interface IFashionTraitsExtractor
{
    Task<TraitsExtractionResult> ExtractTraitsAsync(
        IReadOnlyCollection<FashionImageInput> images,
        CancellationToken cancellationToken = default);
}
// impls: AzureOpenAITraitsExtractor (implements both ITraitsExtractor and
// IFashionTraitsExtractor off the same Azure OpenAI call) | FakeFashionTraitsExtractor

// ---- API #2: storage + auth ----
public interface IGarmentRepository
{
    Task<Garment> AddAsync(Garment garment, CancellationToken ct = default);
    Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default);
}
// impls: AzureCosmosGarmentRepository | InMemoryGarmentRepository
// (Cosmos DB chosen over Azure SQL — container "garments" partitioned on /userId,
// serverless capacity mode in every stage; see stage.bicep)

public interface IImageStore
{
    Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default); // returns URL
    Task DeleteAsync(string key, CancellationToken ct = default);
}
// impls: AzureBlobImageStore | S3ImageStore | InMemoryImageStore
// (garment-images container has blob-level public read — ImageUrl is a stable URL persisted
// forever in Garment, so it can't depend on an expiring SAS; see stage.bicep)

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
// impls: AzureSqlProfileRepository | CosmosProfileRepository | InMemoryProfileRepository

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

// ---- cross-cutting: observability (not tied to one API — see "Observability" below) ----
public interface ILogService
{
    void Log(LogSeverity severity, string message,
        IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null);
}
// impls: LocalLogService | AzureApplicationInsightsLogService

public interface IMetricsService
{
    void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null);
    void RecordValue(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
}
// impls: LocalMetricsService | AzureApplicationInsightsMetricsService

public interface IAlertService
{
    Task RaiseAsync(AlertSeverity severity, string message,
        IReadOnlyDictionary<string, object?>? context = null, CancellationToken ct = default);
}
// impls: LocalAlertService | AzureApplicationInsightsAlertService

public interface ICorrelationContextAccessor
{
    string CorrelationId { get; set; }
    string UserId { get; set; }   // empty until UserContextMiddleware sets it post-auth
}
// impl: AsyncLocalCorrelationContextAccessor — single shared impl, not a Local/Azure choice

// ---- cross-cutting: secrets (used by any provider needing credentials, e.g. AzureOpenAITraitsExtractor) ----
public interface ISecretManager
{
    Task<string> GetSecretAsync(string name, CancellationToken ct = default);
}
// impls: MockSecretManager | AzureKeyVaultSecretManager — see "Secrets & per-stage cloud config" below
```

Project layout: interfaces and domain types (`GarmentTraits`, `Garment`,
`StyleProfile`, `ImageInput`) live in a **Core/Domain** project with **no** SDK
references. Each concrete implementation lives in an **Infrastructure**
project/folder and is the only place an SDK (Azure OpenAI client, EF Core, Blob
SDK) is referenced. Controllers reference Core only. Within Infrastructure,
cloud-specific implementations are further isolated per the "Multi-cloud
folder convention" above.

---

## API contract & OpenAPI (single source of truth for the frontend)  ✅ DONE

The backend is the contract owner. It emits an **OpenAPI 3.0.1 document** that
the frontend (`packages/types` and the API client in `FRONTEND.md`) generates
from, so the DTOs are never hand-copied in two repos and can't silently drift.

### Current setup

- **Library:** Swashbuckle.AspNetCore 6.5.0 (configured in `Program.cs`).
- **Swagger UI:** available at `/swagger` in all environments.
- **Spec endpoint:** `/swagger/v1/swagger.json` — served live from the running API.
- **Committed spec:** `contracts/openapi.json` — checked into the repo so
  frontend codegen doesn't require a running server.
- **XML docs:** enabled (`GenerateDocumentationFile` in `robe.api.csproj`);
  controller summaries and remarks appear in the spec.
- **JWT Bearer security:** defined in the spec as the `Bearer` scheme; Swagger UI
  has an "Authorize" button for pasting tokens.
- **Local-only `X-User-Id` scheme:** when `UseLocalFakes: true`, `Program.cs`
  registers a second security scheme (`X-User-Id`, ApiKey-in-header) as an OR
  alternative to `Bearer`, matching the header `LocalAuthHandler` already
  accepts (see "Local dev auth bypass" in `FRONTEND.md`). Click Authorize in
  Swagger UI, set `X-User-Id` to any value (e.g. `dev-user`), and every "Try it
  out" call authenticates without hand-crafting a JWT. Gated on `UseLocalFakes`
  so it never appears against a deployed (Entra-backed) environment — cloud
  Swagger still only offers `Bearer`.
- **Typed response DTOs:** all controllers use explicit response types from
  `robe.api/Models/ApiResponses.cs` (`AnalyzeResponse`, `GarmentResponse`,
  `GarmentListResponse`, `ProfileResponse`, `RecommendationsResponse`,
  `ErrorResponse`). Every action has `[ProducesResponseType]` attributes so
  Swashbuckle emits accurate status codes and schemas.

### Regenerating the spec

```bash
# start the API, fetch the spec, stop
dotnet run --project robe.api &
sleep 5
curl -s http://localhost:5000/swagger/v1/swagger.json -o contracts/openapi.json
kill %1
```

- **Working rule:** when an endpoint or DTO changes, regenerate
  `contracts/openapi.json` in the same commit — treat a stale spec as a bug.

### Frontend codegen (from `contracts/openapi.json`)

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

## CORS (local dev + deployment)

The web client runs on a different origin than the API (e.g. `localhost:3000`
calling `localhost:5000`), so the API must send CORS headers or the browser
blocks the call at preflight.

```csharp
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? new[] { "http://localhost:3000" })
     .AllowAnyHeader()      // allows Authorization + Content-Type
     .AllowAnyMethod()));   // covers GET/POST/DELETE + the OPTIONS preflight

// pipeline ORDER MATTERS:
app.UseRouting();
app.UseCors("default");     // AFTER UseRouting, BEFORE auth
app.UseAuthentication();
app.UseAuthorization();
```

- **Order is the usual bug**: `UseCors` must precede `UseAuthentication`/
  `UseAuthorization`. Because requests carry an `Authorization` header, the
  browser sends an unauthenticated `OPTIONS` preflight first; if CORS runs after
  auth, that preflight gets a 401 with no CORS headers → "No
  'Access-Control-Allow-Origin'" error. CORS first lets the preflight resolve
  before auth sees it.
- Allowed origins come from **config** (`Cors:Origins`), not hardcoded — set the
  web/app origins per environment.
- Bearer tokens in a header don't need `AllowCredentials`. Only add it for
  cookie-based auth, and then origins must be explicit (no `AllowAnyOrigin`).

---

## Observability: logging, metrics, alerts  ✅ DONE

Cross-cutting (not one of the numbered APIs) — applies across the whole
backend, same interface-first + Local/Azure dual-impl rule as everything else.
See "Dependency model" above for the four interfaces.

### Implementations

- **Local** (`robe.infrastructure/Observability/Local/`) — `LocalLogService`,
  `LocalMetricsService`, `LocalAlertService` write structured entries through
  the standard `ILogger<T>` (console in dev) and also keep a bounded in-memory
  buffer (`RecentEntries`), so the same classes work for local dev *and* test
  assertions — no separate `Fake*` needed. The console line itself is a flat,
  comma-separated `"key":"value"` list (camelCase keys matching the
  Application Insights `Properties` dictionary keys, e.g. `correlationId`,
  `userId`) rather than free-text — e.g.
  `"correlationId":"...", "userId":"user-a", "severity":"Information",
  "message":"...", "category":"top"`. Numeric values (metric `value`,
  `durationMs`) are left unquoted. Any extra `properties`/`tags`/`context`
  dictionary passed to `Log`/`RecordValue`/`RaiseAsync` is appended the same
  way — **working rule**: if you add a new field to one of these services,
  keep emitting it as its own `"key":"value"` pair, not concatenated into the
  message text.
- **Azure** (`robe.infrastructure/Observability/Azure/`) —
  `AzureApplicationInsightsLogService`/`MetricsService`/`AlertService`, built on
  `Microsoft.ApplicationInsights`'s `TelemetryClient`. Pinned to the **2.x**
  package line (`2.22.0`) — the 3.x line pulls transitive packages that target
  newer TFMs than this solution and spam build warnings.
- **`ICorrelationContextAccessor`** has exactly **one** implementation
  (`AsyncLocalCorrelationContextAccessor`, backed by `AsyncLocal<string>`) —
  it's plumbing shared by both Local and Azure, not a provider choice, so it's
  registered once outside the `UseLocalFakes` toggle. It carries both
  `CorrelationId` and `UserId` so every log/metric/alert can be tied back to
  who attempted the call, not just which request.
- **Alarm semantics**: `IAlertService.RaiseAsync` is called explicitly by app
  code when something is wrong (e.g. an Azure OpenAI call failing). It does
  not evaluate thresholds itself — in the Azure impl it raises an `"Alert"`
  event into Application Insights; Azure Monitor alert rules (configured on
  the workspace) own thresholding/routing to Action Groups.

### Wiring — zero controller changes

Instrumentation is added via **decorators**, not by touching controllers
(respects "don't modify already-approved APIs"):
- `ObservableTraitsExtractor`, `ObservableProfileGenerator`,
  `ObservableRecommender`, `ObservableGarmentRepository`,
  `ObservableSecretManager`
  (`robe.infrastructure/Observability/Decorators/`) wrap the real/fake
  implementation, recording duration + success/failure counters + domain
  counters (`garment_traits.analyzed`, `profile.generated`,
  `recommendations.served`, `garments.stored`, `secret.fetched`, ...), and call
  `IAlertService.RaiseAsync` when the wrapped call throws.
- `CorrelationIdMiddleware` (`robe.api/Middleware/`) is the **first**
  middleware in the pipeline (before `UseSwagger`/`UseCors`/auth) so every
  request — including failed CORS preflights and 401s — gets a correlation ID
  and HTTP metrics (`http.requests`, `http.request_duration_ms`). It reads
  `X-Correlation-Id` from the incoming request and reuses it if the caller
  already sent one; otherwise it mints a new GUID. The ID is echoed back on
  the response (`X-Correlation-Id`) and attached to every log/metric/alert
  emitted during that request via `ICorrelationContextAccessor`.
- `UserContextMiddleware` (`robe.api/Middleware/`) runs **after**
  `UseAuthentication`/`UseAuthorization` (claims aren't populated before
  then) and stamps `ICorrelationContextAccessor.UserId` from `ICurrentUser`.
  Because it sits after `UseAuthorization`, it never runs for a request
  `[Authorize]` rejects — those keep `UserId` empty, which is correct (no
  caller was ever authenticated). Everything *deeper* in the pipeline
  (controllers, `Observable*` decorators) correctly sees the `UserId` it sets,
  since `AsyncLocal` flows forward into whatever a middleware calls next.

  **Gotcha — `AsyncLocal` does not flow backward.** `CorrelationIdMiddleware`
  is the *outermost* wrapper; its own "HTTP request completed" log runs in its
  `finally` block, i.e. in **its own continuation after `await _next(context)`
  returns** — that continuation resumes with the `ExecutionContext` as it was
  *before* the call, so it cannot see `UserId` mutations a deeper middleware
  made inside that call. (Compare: `CorrelationIdMiddleware` *can* see its own
  `CorrelationId` in that same log, because it set that value *itself*,
  earlier in its *own* method body — that's just reading a property it wrote,
  not cross-context propagation.) The fix: `CorrelationIdMiddleware` also
  takes `ICurrentUser` and, in its `finally` block, re-checks
  `context.User.Identity?.IsAuthenticated` and re-stamps
  `correlation.UserId` itself before logging — `HttpContext.User` is a plain
  mutable reference, not `AsyncLocal`, so it correctly reflects whatever
  `UseAuthentication` set regardless of continuation boundaries. If you add
  another "wraps the whole pipeline and logs at the end" middleware, you'll
  hit this same trap — don't assume a deeper middleware's
  `ICorrelationContextAccessor` writes are visible to your post-`next()` code.

Both are wired in `Program.cs` under the same `UseLocalFakes` branch as
everything else — e.g. `ITraitsExtractor` is registered as
`new ObservableTraitsExtractor(new AzureOpenAITraitsExtractor(...), log, metrics, alerts)`.

### Config

```jsonc
// appsettings.json
{ "ApplicationInsights": { "ConnectionString": "" } }  // set per environment; empty = no-op locally
```

- **Working rule**: when adding a new external-dependency interface (a new
  LLM/vendor call, a new repository), wrap its DI registration in an
  `Observable*` decorator the same way, rather than adding logging calls
  inside controllers.

---

## Secrets & per-stage cloud config (dev / gamma / live)  ✅ DONE

Cross-cutting (not one of the numbered APIs) — same interface-first +
Local/Azure dual-impl rule as Observability. The API deploys to three real
Azure stages, each fully isolated, so a misconfigured stage can never read
another stage's secrets.

**Interface:** `ISecretManager` (see "Dependency model" above) — a single
`GetSecretAsync(name, ct)` lookup; callers pass colon-namespaced names
(`"AzureOpenAI:ApiKey"`) regardless of which implementation is behind it.

### Implementations

- **Local** (`robe.infrastructure/Secrets/MockSecretManager.cs`) — backed by
  the `LocalSecrets` dictionary in `appsettings.Local.json`. Used whenever
  `UseLocalFakes: true`, and swapped in by every integration test so tests
  never touch a real vault.
- **Azure** (`robe.infrastructure/Secrets/Azure/AzureKeyVaultSecretManager.cs`)
  — `Azure.Security.KeyVault.Secrets.SecretClient` + `Azure.Identity`'s
  `DefaultAzureCredential` (system-assigned managed identity in Azure; falls
  back to `az`/VS credentials for local testing against a real vault). Colon
  names map to Key Vault's allowed charset (`AzureOpenAI:ApiKey` →
  `AzureOpenAI--ApiKey`, same convention as .NET's own `AddAzureKeyVault`
  provider). Results are cached in-memory for 10 minutes so a `Scoped`
  consumer like `AzureOpenAITraitsExtractor` — which reads 3 secrets per
  request — doesn't round-trip to Key Vault on every call. A 404 from Key
  Vault is rethrown as `KeyNotFoundException`, matching `MockSecretManager`'s
  contract. The real `SecretClient` call sits behind an internal
  `ISecretFetcher` seam purely for testability (a hand-written
  `FakeSecretFetcher` in tests — no mocking library, same Fake/Mock
  convention used everywhere else in this repo).
- **`ObservableSecretManager`** wraps either implementation with
  `secret.fetched`/`secret.fetch_failed` metrics and an
  `IAlertService.RaiseAsync` call on failure — see "Observability" above.

### Multi-cloud folder convention

See the working rule of the same name above. In `robe.infrastructure`, every
cloud-specific class so far lives in: `TraitsExtraction/Azure/`,
`Storage/Azure/`, `Profile/Azure/`, `Recommendations/Azure/`,
`Persistence/Azure/`, `Secrets/Azure/`, `Observability/Azure/` — each
namespace mirrors its folder (e.g. `Robe.Infrastructure.Persistence.Azure`).

### Per-stage config: dev / gamma / live

| Stage | Resource group | Key Vault | App Service Plan | `ASPNETCORE_ENVIRONMENT` |
|---|---|---|---|---|
| dev | `rg-robe-dev` | `kv-robeai-dev` | F1 (Free) | `Dev` |
| gamma | `rg-robe-gamma` | `kv-robeai-gamma` | S1 (Standard) | `Gamma` |
| live | `rg-robe-live` | `kv-robeai-live` | P1v3 (PremiumV3) | `Live` |

- `appsettings.Dev.json` / `appsettings.Gamma.json` / `appsettings.Live.json`
  (`robe.api/`) each hold only the non-secret `KeyVault:VaultUri` for that
  stage — the URI is deterministic from the naming convention above, not
  itself sensitive, so it's committed like `Cors:AllowedOrigins` already is.
  Picked up for free by ASP.NET Core's standard
  `appsettings.{ASPNETCORE_ENVIRONMENT}.json` convention; no extra code in
  `Program.cs`. This is separate from `appsettings.Local.json`/
  `UseLocalFakes`, which is local-machine dev with fully mocked secrets and
  never touches a real stage.
- Each stage's App Service has a **system-assigned managed identity** granted
  the `Key Vault Secrets User` RBAC role on **only its own** vault — no
  credentials to store or rotate for Key Vault access itself.
- Infra is Bicep, in `infra/Azure/bicep/`:
  - `modules/stage.bicep` — one stage's full resource set (Key Vault, App
    Service Plan + App Service, Application Insights, the RBAC role
    assignment). Deployable standalone against a matching
    `parameters/<stage>.bicepparam`.
  - `main.bicep` — subscription-scoped; creates all 3 resource groups and
    deploys `stage.bicep` into each.
  - **Bicep never sets secret values** — after deploying a stage, populate its
    vault out-of-band:
    ```bash
    az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--Endpoint --value <...>
    az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--ApiKey --value <...>
    az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--DeploymentName --value <...>
    az keyvault secret set --vault-name kv-robeai-dev --name CosmosDb--Endpoint --value <...>
    az keyvault secret set --vault-name kv-robeai-dev --name CosmosDb--DatabaseName --value robeai
    az keyvault secret set --vault-name kv-robeai-dev --name CosmosDb--ContainerName --value garments
    az keyvault secret set --vault-name kv-robeai-dev --name Storage--BlobServiceUri --value <...>
    az keyvault secret set --vault-name kv-robeai-dev --name Storage--ContainerName --value garment-images
    ```
    (`dev-create.sh` writes the Cosmos/Storage values above automatically from Bicep
    outputs, same as it already does for the OpenAI endpoint — populate gamma/live by
    hand until those stages get their own create script.)
  - Deploy: `az login` → `az account set --subscription <id>` →
    `az deployment sub create --location canadacentral --template-file infra/Azure/bicep/main.bicep`.
  - `infra/Azure/bicep/dev-create.sh` — deploys the dev stage alone, populates
    throwaway secrets, publishes + deploys the API, then smoke tests the Key
    Vault/managed identity/RBAC wiring via the unauthenticated
    `POST /api/garments/analyze`. Leaves the stage running (`--yes` skips the
    deploy confirm prompt) so you can iterate against it. If `az` CLI isn't
    found it asks for explicit consent before installing it (winget/brew/
    Microsoft's Linux script) — asked even under `--yes`, since installing
    software is a bigger ask than skipping a confirmation. Picks which
    subscription to deploy into via `--subscription=<id-or-name>`, or an
    interactive numbered picker from `az account list` when neither
    `--subscription` nor `--yes` is given (with `--yes` alone it just uses
    whatever's currently active).
  - `infra/Azure/bicep/dev-teardown.sh` — deletes `rg-robe-dev` so dev stops
    accruing cost between runs. Run it once you're done. `--yes` skips the
    confirm prompt, `--wait` blocks until the delete finishes, `--purge-vault`
    also purges the soft-deleted `kv-robeai-dev` so the name is reusable
    immediately instead of staying reserved for up to 90 days. `--purge-openai`
    does the same for the Azure OpenAI account (`aoai-robe-dev`) — its custom
    subdomain is also soft-deleted for 48 hours and causes a `CustomDomainInUse`
    error on the next `dev-create.sh` if not purged. The script auto-detects
    the region the account was deployed in, so this works even if `openAiLocation`
    changed between runs. **Recommended teardown command:**
    `./dev-teardown.sh --yes --wait --purge-vault --purge-openai`

### Azure OpenAI model versioning

The vision model (used by `AzureOpenAITraitsExtractor` and related implementations) is
configured in two variables at the top of `infra/Azure/bicep/modules/stage.bicep`:

```bicep
var visionModelName    = 'gpt-5-mini'   // model family
var visionModelVersion = '2025-08-07'   // bump when upgrading
```

The deployment resource name in `stage.bicep` is `'gpt-5-mini'`. Changing these two lines +
renaming the deployment resource redeploys all stages on the next `dev-create.sh` or
`az deployment` run.

**Region note:** the dev Azure OpenAI account is in `eastus` (set via `openAiLocation = 'eastus'`
in `parameters/dev.bicepparam`), not `canadacentral` where the App Service and Key Vault live.
GlobalStandard quota is region-specific and was unavailable in canadacentral for this subscription
type — eastus has the broadest quota across subscription types.

**Checking available versions** when a deploy fails with `ServiceModelDeprecating`:
```bash
az cognitiveservices model list --location eastus -o json \
  | python3 -c "import json,sys; [print(m['model']['name'], m['model']['version']) for m in json.load(sys.stdin) if 'gpt-5' in m.get('model',{}).get('name','')]" | sort -u
```
Look for versions without a `replacementConfig` / `autoUpgradeStartDate` already past.

**Known deprecated model versions (new deployments blocked as of July 2026):**
- `gpt-4o/2024-11-20`, `gpt-4o/2024-08-06`, `gpt-4o/2024-05-13` — all `ServiceModelDeprecating`
- `gpt-4.1/2025-04-14` — entered deprecating state July 2026

**Azure AI Foundry (`kind: 'AIServices'`) vs classic Azure OpenAI (`kind: 'OpenAI'`):** we
deliberately kept `kind: 'OpenAI'` in `stage.bicep`. Switching to `kind: 'AIServices'`
(a Foundry resource) does **not** bypass model deprecation — deprecation is enforced at
the model+version level regardless of resource kind. There is no other compelling reason
to switch right now; `kind: 'OpenAI'` is simpler and sufficient.

---

## Infra setup: CI pipeline for dev stage  ⬜ TODO (planned, not yet built)

Goal: an on-demand + daily pipeline, checked into git, that creates the dev
stage, runs tests, certifies it, and tears it down — so any dev can depend on
it to verify changes throughout the day without anyone babysitting cost.
Captured here so the design isn't lost; **do not build this until explicitly
asked to** (same rule as "Planned APIs" below).

- **Platform: GitHub Actions**, not Azure DevOps — this repo is hosted at
  `github.com/ekaag/robe.ai`, so Actions needs no new infra to stand up and
  lives next to the existing PR/`gh` workflow.
- **Triggers**: `workflow_dispatch` (on-demand — any dev can fire it manually
  or via `gh workflow run`) **and** `schedule` (daily cron).
- **Auth**: OIDC federated credentials (`azure/login` with `id-token: write`)
  rather than a long-lived service principal secret stored in GitHub — nothing
  to leak or rotate.
- **Concurrency group** on the workflow so an on-demand run and the daily cron
  can't collide on the same shared `rg-robe-dev`.
- **Runner**: `ubuntu-latest` (GitHub-hosted Linux) — this is also why
  `dev-create.sh`/`dev-teardown.sh` are bash rather than PowerShell: Linux
  runners are the default/cheapest on GitHub Actions (Windows runners cost
  ~2x the minutes), and bash runs there unmodified.
- **Steps**: `dev-create.sh --yes` (deploy + the smoke test it already runs)
  → certify → `dev-teardown.sh --yes` gated on `if: always()` so teardown
  still runs even if certify fails — that's what actually guarantees no
  leaked cost.
- **Open question to resolve before building**: should "certify" be just the
  smoke test already embedded in `dev-create.sh`, or also the full `dotnet
  test` xUnit suite run against the deployed instance? The latter needs the
  suite pointed at a real HTTP endpoint instead of the in-process
  `WebApplicationFactory` it uses today — not yet designed.

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
{ "traits": { /* GarmentTraits object */ }, "modelVersion": "azure-openai-gpt-5-mini" }
```

Errors: `400` (bad/missing image), `422` (no garment detected), `502` (model
call failed).

Implementation notes: send the image to the Azure OpenAI vision model (gpt-5-mini)
via the Chat Completions API, using **JSON mode** (`ChatResponseFormat.CreateJsonObjectFormat()`)
with a prompt that demands JSON matching the GarmentTraits schema and nothing else;
still parse defensively (strip any code fences, validate against the schema,
reject/repair on parse failure). JSON Object mode is model-agnostic and works
with gpt-5-mini; upgrading to schema-constrained Structured Outputs
(`CreateJsonSchemaFormat`) would give stronger guarantees but is not required.

Tests to show passing:
- valid image → well-formed `GarmentTraits` with required fields populated
- non-clothing image → `422`
- missing/oversized image → `400`
- malformed model output → handled gracefully, no 500 leak

### API #1 (extension) — multi-image, multi-person fashion extraction  ✅ DONE

`ITraitsExtractor` above returns one garment from one image with no concept of
"whose garment is this." Real wardrobe photos are often group shots or outfit
flat-lays with several people, so `AzureOpenAITraitsExtractor` also implements a
second interface, `IFashionTraitsExtractor`, over the same Azure OpenAI call —
returning traits **per person per image**, across up to `MaxImages` images in one
request. Stateless, same as `/analyze`: no DB, no auth required.

**Depends on (inject):** `IFashionTraitsExtractor`

**`POST /api/garments/analyze-batch`**

Request:
```jsonc
{
  "images": [
    { "imageId": "img-1", "imageBase64": "<...>", "mimeType": "image/jpeg" },
    { "imageId": "img-2", "imageBase64": "<...>", "mimeType": "image/jpeg" }
  ]
}
```
`imageId` is optional per image — auto-assigned as `img-1`, `img-2`, ... if omitted.
Max 10 images per request (`MaxImagesPerBatch`), each ≤ 10 MB, `image/jpeg` |
`image/png` | `image/webp` only.

Response `200`:
```jsonc
{
  "images": [
    {
      "imageId": "img-1",
      "people": [
        {
          "personId": "person_1",
          "position": "center",
          "overallStyle": ["casual"],
          "styleTags": ["casual", "minimalist"],
          "faceBoundingBox": { "x": 0.42, "y": 0.05, "width": 0.16, "height": 0.18 },
          "clothingItems": [
            {
              "category": "top",
              "type": "t-shirt",
              "subtype": null,
              "primaryColor": { "normalized": "blue", "shade": "navy blue" },
              "secondaryColors": [],
              "pattern": "solid",
              "material": "cotton-like",
              "fit": "regular",
              "length": null, "sleeveLength": "short", "neckline": "round",
              "collarType": null, "waistRise": null, "closureType": null,
              "details": [], "visibleText": null, "brand": null, "logo": null,
              "condition": "good",
              "styleTags": ["casual"],
              "confidence": 0.92,
              "boundingBox": { "x": 0.30, "y": 0.22, "width": 0.40, "height": 0.35 }
            }
          ],
          "overallConfidence": 0.90
        }
      ],
      "warnings": []
    }
  ],
  "modelVersion": "azure-openai-gpt-5-mini"
}
```
`category` covers `top | bottom | dress | skirt | outerwear | footwear | headwear
| accessory | one-piece | traditional-wear | unknown` — including South Asian
garments (saree, kurta, kurti, salwar, lehenga, sherwani, dhoti, ...) with
saree-specific detail (body/border/pallu color, zari, drape style) captured via
the free-form `type`/`details` fields. This is a superset of the `GarmentTraits`
category enum, not a replacement — `ITraitsExtractor.ExtractAsync` (single-image
API) still maps into the narrower `GarmentTraits.Category` enum via
`ParseCategory`.

**People vs. faces — deliberate privacy boundary.** The extractor detects that a
person is present (to group clothing items per person) but is explicitly barred
from identity inference. The system prompt enforces:
```
- Do NOT identify, recognize, or name any individual person.
- Do NOT perform face recognition.
- Do NOT infer ethnicity, religion, health status, sexual orientation, or any sensitive attribute.
```
A person is represented only as `personId` (`person_1`, `person_2`, ... — stable
only *within* one image, not across images), coarse `position`
(`left | center | right | upper-left | upper-right | lower-left | lower-right`),
and — see below — a `faceBoundingBox` that localizes but never identifies. No
face embeddings, no biometric data, no demographic attributes are extracted or
stored. **Working rule**: if this prompt is ever edited, keep these constraints —
they're a privacy boundary, not incidental wording.

**Bounding boxes (`faceBoundingBox`, `boundingBox`) — for UI overlays.** Both are
normalized (0.0–1.0, top-left origin) `{ x, y, width, height }`, resolution-
independent so the caller scales to whatever size the image is actually
rendered at. `faceBoundingBox` on `PersonTraits` marks *where a face is*, not
who it belongs to — this is face **localization**, which the privacy boundary
above permits; face **recognition/identification** is what's barred. `boundingBox`
on `ClothingItemTraits` marks the garment's extent. Both are nullable — omitted
when the model can't localize confidently — and prompted from the same gpt-5-mini
call rather than a dedicated grounding/object-detection service, so treat them as
**approximate**: good enough to draw a highlight box on screen, not for pixel-
precise crops. Domain type: `BoundingBox(double X, double Y, double Width, double Height)`
in `Robe.Core.Domain`.

Errors: `400` (bad/missing/oversized/too-many images), `502` (model call failed,
malformed model output, or content filtered).

Tests to show passing (`GarmentBatchAnalyzeTests.cs`):
- valid images → `200` with per-image, per-person, per-garment results
- results contain people and clothing items with expected fields populated
- omitted `imageId` → auto-assigned (`img-1`, `img-2`, ...)
- face/garment bounding boxes present and normalized within `[0,1]`
- omitted bounding box → `null`, not an error
- empty/missing images list → `400`
- too many images (> `MaxImagesPerBatch`) → `400`
- invalid base64 / oversized image / unsupported mime type → `400`
- model call failure / malformed model output / content filtered → `502`

New domain types (`Robe.Core.Domain`): `FashionImageInput`,
`TraitsExtractionResult`, `ImageTraitsResult`, `PersonTraits`,
`ClothingItemTraits`, `ColorTraits`, `BoundingBox` — see `IFashionTraitsExtractor`
above for the interface. Not aliases of `GarmentTraits`/`ImageInput`; kept as
separate types since this extraction is richer (per-person, more garment
attributes, spatial location) than what API #2's storage model (`GarmentTraits`)
needs — `GarmentTraits` intentionally has no bounding box field.

### Frontend: visual overlay (faces + garments on the image)  ✅ DONE

`apps/web/components/ImageWithOverlay.tsx` renders an image with an absolutely-
positioned SVG layer drawing a dashed box per `faceBoundingBox` and a solid,
labeled box per garment `boundingBox`. Since the container's aspect ratio rarely
matches the image's natural aspect ratio, it replicates the CSS
`object-fit: contain` letterboxing math itself (`ResizeObserver` for container
size, `<img onLoad>` for natural size) so normalized coordinates land on the
correct pixel rect rather than the naive `container × normalized` calculation,
which would be wrong whenever the image is letterboxed.

Wired into `BatchReview` (`UploadFlow.tsx`) in place of the old 52×52 thumbnail —
each image in the batch-review step now shows the full preview with overlays,
and a clothing item's box is highlighted (`highlighted: true`) when its checkbox
in the list below is selected.

**Update:** the web app's `UploadFlow.tsx` no longer has a separate single-image
code path at all — it now always calls `/analyze-batch` (a single photo just
yields a one-image batch result), so every web upload goes through
`ImageWithOverlay`. See "Web upload consolidation" below. `GarmentTraits` still
has no bounding box field — `/analyze` (`ITraitsExtractor`) is untouched and
still backs the mobile app.

### Web upload consolidation: single image now also uses `/analyze-batch`  ✅ DONE

The web app previously had two client-side code paths in `UploadFlow.tsx`: a
single-image one calling `useAnalyzeGarment` (`/api/garments/analyze`) with its
own "review" step (`TraitRow`/`FormalityDots`/`ConfidenceBar`), and a batch one
calling `useAnalyzeBatch` for 2+ files. These duplicated the same job — pick a
photo, extract traits, let the user confirm, save — so the web app now always
calls `useAnalyzeBatch`, even for a single file; there's one `handleFileChange`
path and one review UI (`BatchReview`) regardless of file count.

- **Backend is unchanged**: `POST /api/garments/analyze` and `ITraitsExtractor`
  still exist — this was a web-app-only consolidation, not a backend one. They
  still back **`apps/mobile/components/UploadFlow.tsx`** (single-image only, no
  batch support) and **`infra/Azure/bicep/dev-create.sh`**'s smoke test, both of
  which call `/analyze` directly. **Working rule**: don't remove `/analyze`,
  `ITraitsExtractor`, `FakeTraitsExtractor`, `ObservableTraitsExtractor`, or
  `GarmentAnalyzeTests.cs` without also migrating those two consumers — they
  were deliberately left out of scope when this consolidation was done.
- `apps/web/vitest.setup.ts` gained a no-op `ResizeObserver` polyfill (jsdom
  doesn't have one) — needed once `ImageWithOverlay` started rendering in every
  upload path, not just the batch-only one, since any test that renders
  `UploadFlow`'s review step now renders it too.
- Fixed a pre-existing bug found along the way: `WardrobePage.test.tsx`'s
  upload-flow tests queried `getByLabelText("Upload garment photo")`
  (singular) against an input actually labeled `"Upload garment photos"`
  (plural) — silently broken before this change (the assertion never matched
  regardless of which endpoint was called). Fixed as part of rewriting those
  tests for the unified flow.

Not yet regenerated automatically: `packages/types/src/index.ts` and
`packages/api/src/*` are **hand-maintained**, not consumed from
`pnpm gen:api`'s output (`packages/types/src/generated.ts`,
`packages/api/src/generated-client.ts`) despite "API contract & OpenAPI" above
describing the frontend as codegening from the spec — that pipeline exists
(and its broken `orval.config.ts` relative path was fixed while adding this
feature) but the app doesn't consume its output yet. `BoundingBox` was added
by hand to `packages/types/src/index.ts` to match. Reconciling the two is
out of scope here — flagging so it isn't mistaken for already wired up.

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