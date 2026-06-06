using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Auth — LocalAuthHandler reads X-User-Id header for local dev and tests.
// Replace AddScheme with .AddJwtBearer(...) and configure Entra External ID for production.
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = LocalAuthHandler.SchemeName;
    o.DefaultChallengeScheme = LocalAuthHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, LocalAuthHandler>(LocalAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

if (builder.Configuration.GetValue<bool>("UseLocalFakes"))
{
    var secrets = builder.Configuration
        .GetSection("LocalSecrets")
        .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

    builder.Services.AddSingleton<ISecretManager>(new MockSecretManager(secrets));
    builder.Services.AddScoped<ITraitsExtractor, FakeTraitsExtractor>();
    builder.Services.AddSingleton<IGarmentRepository, InMemoryGarmentRepository>();
    builder.Services.AddSingleton<IImageStore, InMemoryImageStore>();
    builder.Services.AddScoped<IProfileGenerator, FakeProfileGenerator>();
    builder.Services.AddSingleton<IProfileRepository, InMemoryProfileRepository>();
}
else
{
    builder.Services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
    builder.Services.AddScoped<ITraitsExtractor, AzureOpenAITraitsExtractor>();
    builder.Services.AddScoped<IGarmentRepository, SqlGarmentRepository>();
    builder.Services.AddScoped<IImageStore, AzureBlobImageStore>();
    builder.Services.AddScoped<IProfileGenerator, AzureOpenAIProfileGenerator>();
    builder.Services.AddScoped<IProfileRepository, SqlProfileRepository>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
