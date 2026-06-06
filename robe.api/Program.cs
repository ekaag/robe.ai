using System.Text.Json.Serialization;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Secrets;
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

if (builder.Configuration.GetValue<bool>("UseLocalFakes"))
{
    var secrets = builder.Configuration
        .GetSection("LocalSecrets")
        .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

    builder.Services.AddSingleton<ISecretManager>(new MockSecretManager(secrets));
    builder.Services.AddScoped<ITraitsExtractor, FakeTraitsExtractor>();
}
else
{
    builder.Services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
    builder.Services.AddScoped<ITraitsExtractor, AzureOpenAITraitsExtractor>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

public partial class Program { }
