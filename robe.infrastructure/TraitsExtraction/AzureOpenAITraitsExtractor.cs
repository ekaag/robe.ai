using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction;

public class AzureOpenAITraitsExtractor : ITraitsExtractor
{
    private readonly ISecretManager _secrets;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public AzureOpenAITraitsExtractor(ISecretManager secrets) => _secrets = secrets;

    public async Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default)
    {
        var endpoint = await _secrets.GetSecretAsync("AzureOpenAI:Endpoint", ct);
        var apiKey = await _secrets.GetSecretAsync("AzureOpenAI:ApiKey", ct);
        var deployment = await _secrets.GetSecretAsync("AzureOpenAI:DeploymentName", ct);

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        var chatClient = client.GetChatClient(deployment);

        var base64 = Convert.ToBase64String(image.Data);
        var dataUrl = $"data:{image.MimeType};base64,{base64}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart("Analyze this garment image and return the JSON."),
                ChatMessageContentPart.CreateImagePart(new Uri(dataUrl), ChatImageDetailLevel.High))
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion;
        try
        {
            completion = await chatClient.CompleteChatAsync(messages, options, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExtractionException("Azure OpenAI call failed.", ex);
        }

        return ParseTraits(completion.Content[0].Text);
    }

    private static GarmentTraits ParseTraits(string raw)
    {
        var json = StripCodeFences(raw.Trim());

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("garmentDetected", out var flag) && !flag.GetBoolean())
                throw new GarmentNotDetectedException();

            var traits = JsonSerializer.Deserialize<GarmentTraits>(json, JsonOptions)
                ?? throw new TraitsParseException("Model returned null after deserialization.");

            if (traits.PrimaryColor is null || string.IsNullOrWhiteSpace(traits.Subcategory))
                throw new TraitsParseException("Required fields (primaryColor, subcategory) missing from model output.");

            return traits;
        }
        catch (GarmentNotDetectedException) { throw; }
        catch (TraitsParseException) { throw; }
        catch (Exception ex)
        {
            throw new TraitsParseException("Failed to parse model output as GarmentTraits.", ex);
        }
    }

    private static string StripCodeFences(string json)
    {
        if (!json.StartsWith("```")) return json;
        var lines = json.Split('\n');
        return string.Join('\n', lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```"))).Trim();
    }

    private const string SystemPrompt = """
        You are a garment analysis assistant. Analyze the clothing image and return ONLY a single JSON object — no prose, no markdown, no code fences.

        If a garment IS visible, return exactly this shape:
        {
          "category": "top|bottom|dress|outerwear|footwear|accessory|other",
          "subcategory": "e.g. t-shirt, jeans, sneakers",
          "primaryColor": { "name": "color name", "hex": "#rrggbb" },
          "secondaryColors": [ { "name": "color name", "hex": "#rrggbb" } ],
          "pattern": "solid|striped|plaid|checked|floral|graphic|other",
          "material": "inferred material or null",
          "fit": "slim|regular|loose|oversized or null",
          "formality": 1,
          "seasonality": ["spring","summer","fall","winter","all"],
          "styleTags": ["minimalist","classic","casual","streetwear","formal","bohemian","sporty","vintage","preppy","edgy","romantic"],
          "occasions": ["everyday","work","formal","sport","event"],
          "confidence": 0.9
        }

        If NO garment is visible in the image, return ONLY: {"garmentDetected": false}
        """;
}
