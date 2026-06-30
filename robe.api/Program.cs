using System.Text.Json.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using Robe.Api.Middleware;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Observability;
using Robe.Infrastructure.Observability.Azure;
using Robe.Infrastructure.Observability.Decorators;
using Robe.Infrastructure.Observability.Local;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
var useLocalFakes = builder.Configuration.GetValue<bool>("UseLocalFakes");

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Robe.AI — Wardrobe Style API",
        Version = "v1",
        Description = "Garment trait extraction, wardrobe management, style profiling, and recommendations."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your Entra ID JWT token."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Local dev only: LocalAuthHandler also accepts a plain X-User-Id header
    // (see robe.infrastructure/Auth/LocalAuthHandler.cs), so expose it as an
    // alternate "Authorize" option in Swagger UI — avoids hand-crafting a fake
    // JWT just to click around locally. Gated on UseLocalFakes so this scheme
    // never appears against a deployed (Entra-backed) environment.
    if (useLocalFakes)
    {
        options.AddSecurityDefinition("X-User-Id", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = LocalAuthHandler.UserIdHeader,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Local dev only: set a dev user id (e.g. \"dev-user\") to authenticate without a real JWT."
        });

        // A separate AddSecurityRequirement entry is an OR alternative to the
        // Bearer requirement above (OpenAPI: items within one requirement are
        // ANDed, items across the array are ORed) — so either credential works.
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "X-User-Id"
                    }
                },
                Array.Empty<string>()
            }
        });
    }

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
builder.Services.AddHttpContextAccessor();

// Auth — JWT Bearer when Entra config is present; LocalAuthHandler (X-User-Id header)
// for local dev and integration tests that don't need real tokens.
var entraAuthority = builder.Configuration["Entra:Authority"];
var entraClientId  = builder.Configuration["Entra:ClientId"];

if (!useLocalFakes && !string.IsNullOrEmpty(entraAuthority) && !string.IsNullOrEmpty(entraClientId))
{
    // Validates the ID token the frontend sends as the Bearer credential.
    // Audience = client ID because CIAM ID tokens carry aud = client_id.
    // ValidateIssuer = false: CIAM's friendly-URL authority
    // (vestraoauth.ciamlogin.com) reports a different issuer in its discovery
    // document (ed1aa306-…ciamlogin.com) than the iss claim in tokens it issues.
    // Signature + audience + expiry validation still applies.
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer(options =>
        {
            options.Authority = entraAuthority;
            options.Audience  = entraClientId;
            options.MapInboundClaims = false; // keep raw claim names (sub, oid, name)
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
            };
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnAuthenticationFailed = ctx =>
                {
                    Console.Error.WriteLine(
                        $"[JWT] Auth failed — {ctx.Exception.GetType().Name}: {ctx.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = ctx =>
                {
                    var oid = ctx.Principal?.FindFirst("oid")?.Value ?? "(no oid)";
                    Console.Error.WriteLine($"[JWT] Token validated OK, oid={oid}");
                    return Task.CompletedTask;
                },
            };
        });
}
else
{
    builder.Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = LocalAuthHandler.SchemeName;
        o.DefaultChallengeScheme    = LocalAuthHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, LocalAuthHandler>(LocalAuthHandler.SchemeName, _ => { });
}

builder.Services.AddAuthorization();

var allowedOrigins = (builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? Array.Empty<string>())
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .ToArray();

if (allowedOrigins.Length == 0)
    allowedOrigins = new[] { "http://localhost:3000", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition", "Location");
    });
});

builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Correlation ID is plumbing shared by both Local and Azure observability —
// not a Local/Azure choice, so it's registered once outside the toggle below.
builder.Services.AddSingleton<ICorrelationContextAccessor, AsyncLocalCorrelationContextAccessor>();

if (useLocalFakes)
{
    var secrets = builder.Configuration
        .GetSection("LocalSecrets")
        .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

    builder.Services.AddSingleton<ISecretManager>(new MockSecretManager(secrets));

    builder.Services.AddSingleton<ILogService, LocalLogService>();
    builder.Services.AddSingleton<IMetricsService, LocalMetricsService>();
    builder.Services.AddSingleton<IAlertService, LocalAlertService>();

    builder.Services.AddScoped<ITraitsExtractor>(sp => new ObservableTraitsExtractor(
        FakeTraitsExtractor.ReturnsDefault(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddSingleton<IGarmentRepository>(sp => new ObservableGarmentRepository(
        new InMemoryGarmentRepository(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>()));
    builder.Services.AddSingleton<IImageStore, InMemoryImageStore>();
    builder.Services.AddScoped<IProfileGenerator>(sp => new ObservableProfileGenerator(
        new FakeProfileGenerator(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddSingleton<IProfileRepository, InMemoryProfileRepository>();
    builder.Services.AddScoped<IRecommender>(sp => new ObservableRecommender(
        new FakeRecommender(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddSingleton<IInventoryRepository>(new InMemoryInventoryRepository());
}
else
{
    builder.Services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();

    var telemetryConfig = TelemetryConfiguration.CreateDefault();
    var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
        telemetryConfig.ConnectionString = aiConnectionString;
    builder.Services.AddSingleton(new TelemetryClient(telemetryConfig));

    builder.Services.AddSingleton<ILogService, ApplicationInsightsLogService>();
    builder.Services.AddSingleton<IMetricsService, ApplicationInsightsMetricsService>();
    builder.Services.AddSingleton<IAlertService, ApplicationInsightsAlertService>();

    builder.Services.AddScoped<ITraitsExtractor>(sp => new ObservableTraitsExtractor(
        new AzureOpenAITraitsExtractor(sp.GetRequiredService<ISecretManager>()),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddScoped<IGarmentRepository>(sp => new ObservableGarmentRepository(
        new SqlGarmentRepository(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>()));
    builder.Services.AddScoped<IImageStore, AzureBlobImageStore>();
    builder.Services.AddScoped<IProfileGenerator>(sp => new ObservableProfileGenerator(
        new AzureOpenAIProfileGenerator(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddScoped<IProfileRepository, SqlProfileRepository>();
    builder.Services.AddScoped<IRecommender>(sp => new ObservableRecommender(
        new AzureOpenAIRecommender(),
        sp.GetRequiredService<ILogService>(),
        sp.GetRequiredService<IMetricsService>(),
        sp.GetRequiredService<IAlertService>()));
    builder.Services.AddScoped<IInventoryRepository, SqlInventoryRepository>();
}

var app = builder.Build();

// First in the pipeline so every request — including CORS preflights and 401s —
// gets a correlation ID and HTTP metrics, before any other middleware can short-circuit it.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Robe.AI API v1");
});

app.UseRouting();
app.UseCors("AllowConfiguredOrigins");

// Catch unhandled exceptions here (after CORS) so error responses still carry
// Access-Control-Allow-Origin — prevents the browser from misreporting server
// errors as CORS failures.
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex)
    {
        var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            await context.Response.WriteAsJsonAsync(isDev
                ? new { error = ex.GetType().Name, detail = ex.Message, stack = ex.StackTrace }
                : (object)new { error = "Internal server error." });
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

// After auth so claims are populated — stamps the correlation context with
// the caller's user id before any controller/decorator emits telemetry.
app.UseMiddleware<UserContextMiddleware>();

app.MapControllers();
app.Run();

public partial class Program { }
