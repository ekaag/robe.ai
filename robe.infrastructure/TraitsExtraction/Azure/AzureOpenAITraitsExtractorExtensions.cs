using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction.Azure;

/// <summary>
/// DI registration helpers for <see cref="AzureOpenAITraitsExtractor"/>.
/// Call <see cref="AddAzureOpenAITraitsExtractor"/> in the non-local-fakes branch
/// of <c>Program.cs</c>, then wrap the resulting singleton with
/// <c>ObservableTraitsExtractor</c> (for <see cref="ITraitsExtractor"/>) and
/// <c>ObservableFashionTraitsExtractor</c> (for <see cref="IFashionTraitsExtractor"/>)
/// as usual — this method only registers the concrete Azure implementation, not
/// either public interface, so Program.cs decides observability wrapping for both.
/// </summary>
public static class AzureOpenAITraitsExtractorExtensions
{
    /// <summary>
    /// Registers <see cref="AzureOpenAIOptions"/>, the internal
    /// <see cref="IAzureOpenAIChatAdapter"/>, and
    /// <see cref="AzureOpenAITraitsExtractor"/> as singletons. Does not register
    /// <see cref="ITraitsExtractor"/> or <see cref="IFashionTraitsExtractor"/> —
    /// Program.cs wires both, each behind its own Observable decorator.
    /// </summary>
    public static IServiceCollection AddAzureOpenAITraitsExtractor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureOpenAIOptions>(configuration.GetSection("AzureOpenAI"));

        // Endpoint and deployment name come from Key Vault via ISecretManager (fetched lazily
        // on first call and cached). ISecretManager must already be registered before calling this.
        services.AddSingleton<IAzureOpenAIChatAdapter>(sp =>
            new AzureOpenAIChatAdapter(sp.GetRequiredService<ISecretManager>()));

        // Factory required: DI auto-resolve only finds public constructors, and the
        // constructor is intentionally internal to hide the IAzureOpenAIChatAdapter seam.
        services.AddSingleton<AzureOpenAITraitsExtractor>(sp => new AzureOpenAITraitsExtractor(
            sp.GetRequiredService<IOptions<AzureOpenAIOptions>>(),
            sp.GetRequiredService<ILogger<AzureOpenAITraitsExtractor>>(),
            sp.GetRequiredService<IAzureOpenAIChatAdapter>()));

        return services;
    }
}
