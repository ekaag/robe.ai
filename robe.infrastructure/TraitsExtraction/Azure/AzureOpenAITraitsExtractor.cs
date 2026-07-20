using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction.Azure;

/// <summary>
/// Production traits extractor backed by an Azure OpenAI vision model (gpt-4.1 or later).
///
/// Implements two interfaces:
///   <see cref="ITraitsExtractor"/>        — single garment image → <see cref="GarmentTraits"/> (existing API #1)
///   <see cref="IFashionTraitsExtractor"/> — multi-image, multi-person → <see cref="TraitsExtractionResult"/>
///
/// Authentication: <see cref="Azure.Identity.DefaultAzureCredential"/> (Managed Identity on
/// App Service; Azure CLI / Visual Studio credentials during local development).
/// No API keys in source code or configuration files.
///
/// Retry policy: delegated to the Azure SDK's built-in pipeline (HTTP 429/408/5xx with
/// exponential backoff) to avoid nested retry amplification.
/// </summary>
public sealed class AzureOpenAITraitsExtractor : ITraitsExtractor, IFashionTraitsExtractor
{
    private static readonly ActivitySource Tracer =
        new("Robe.Infrastructure.TraitsExtraction");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<string> SupportedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private readonly IOptions<AzureOpenAIOptions> _options;
    private readonly ILogger<AzureOpenAITraitsExtractor> _logger;
    private readonly IAzureOpenAIChatAdapter _chat;

    // Internal constructor: IAzureOpenAIChatAdapter is an internal seam.
    // Use AddAzureOpenAITraitsExtractor() or TestHelpers.Create() (tests) to instantiate.
    internal AzureOpenAITraitsExtractor(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAITraitsExtractor> logger,
        IAzureOpenAIChatAdapter chat)
    {
        _options = options;
        _logger = logger;
        _chat = chat;
    }

    // -------------------------------------------------------------------------
    // ITraitsExtractor — existing API #1 (single garment image → GarmentTraits)
    // -------------------------------------------------------------------------

    public async Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateSingleImage(image.Data, image.MimeType);

        var fashionImage = new FashionImageInput("image_0", image.Data, image.MimeType);
        var result = await ExtractTraitsAsync(new[] { fashionImage }, ct);

        var primaryItem = result.Images.FirstOrDefault()?.People.FirstOrDefault()?.ClothingItems.FirstOrDefault();
        if (primaryItem is null)
            throw new GarmentNotDetectedException("No person or garment detected in the image.");

        return MapToGarmentTraits(primaryItem);
    }

    // -------------------------------------------------------------------------
    // IFashionTraitsExtractor — multi-image, multi-person fashion extraction
    // -------------------------------------------------------------------------

    public async Task<TraitsExtractionResult> ExtractTraitsAsync(
        IReadOnlyCollection<FashionImageInput> images,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);

        var opts = _options.Value;
        ValidateImageCollection(images, opts.MaxImages, opts.MaxImageSizeBytes);

        var imageIds = images.Select(i => i.ImageId).ToList();

        using var activity = Tracer.StartActivity("ExtractFashionTraits");
        activity?.SetTag("image.count", images.Count);

        _logger.LogInformation(
            "Fashion traits extraction starting — {ImageCount} image(s): [{ImageIds}]",
            images.Count, string.Join(", ", imageIds));

        var sw = Stopwatch.StartNew();

        try
        {
            var request = BuildRequest(images);
            var response = await _chat.CompleteChatAsync(request, cancellationToken);

            if (response.IsContentFiltered)
            {
                _logger.LogWarning("Content filter triggered — images: [{ImageIds}]", string.Join(", ", imageIds));
                activity?.SetTag("content.filtered", true);
                throw new ContentFilterException();
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                _logger.LogWarning("Empty response from Azure OpenAI — images: [{ImageIds}]", string.Join(", ", imageIds));
                throw new ExtractionException("Azure OpenAI returned an empty response.");
            }

            var result = ParseResponse(response.Content, imageIds);

            sw.Stop();
            activity?.SetTag("success", true);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);
            _logger.LogInformation(
                "Fashion traits extraction completed — {ImageCount} image(s) in {ElapsedMs}ms",
                images.Count, sw.ElapsedMilliseconds);

            return result;
        }
        catch (ContentFilterException) { throw; }
        catch (TraitsParseException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetTag("success", false);
            activity?.SetTag("error", ex.GetType().Name);
            _logger.LogError(ex,
                "Fashion traits extraction failed after {ElapsedMs}ms — images: [{ImageIds}]",
                sw.ElapsedMilliseconds, string.Join(", ", imageIds));
            throw new ExtractionException("Azure OpenAI call failed during fashion traits extraction.", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    private static void ValidateSingleImage(byte[] data, string mimeType)
    {
        if (data is null || data.Length == 0)
            throw new ImageValidationException("Image has no content.");
        if (!SupportedMimeTypes.Contains(mimeType))
            throw new ImageValidationException(
                $"Unsupported MIME type '{mimeType}'. Supported: {string.Join(", ", SupportedMimeTypes)}.");
    }

    private static void ValidateImageCollection(
        IReadOnlyCollection<FashionImageInput> images,
        int maxImages,
        long maxBytes)
    {
        if (images.Count == 0)
            throw new ImageValidationException("At least one image must be provided.");
        if (images.Count > maxImages)
            throw new ImageValidationException(
                $"Too many images: {images.Count} provided, maximum is {maxImages}.");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var img in images)
        {
            if (string.IsNullOrWhiteSpace(img.ImageId))
                throw new ImageValidationException("Each image must have a non-empty ImageId.");
            if (!seenIds.Add(img.ImageId))
                throw new ImageValidationException($"Duplicate ImageId: '{img.ImageId}'.");
            if (img.Content is null || img.Content.Length == 0)
                throw new ImageValidationException($"Image '{img.ImageId}' has no content.");
            if (!SupportedMimeTypes.Contains(img.ContentType))
                throw new ImageValidationException(
                    $"Image '{img.ImageId}' has unsupported MIME type '{img.ContentType}'.");
            if (img.Content.Length > maxBytes)
                throw new ImageValidationException(
                    $"Image '{img.ImageId}' ({img.Content.Length / (1024 * 1024.0):F1} MB) " +
                    $"exceeds the {maxBytes / (1024 * 1024)} MB limit.");
        }
    }

    // -------------------------------------------------------------------------
    // Request construction
    // -------------------------------------------------------------------------

    private static ChatAdapterRequest BuildRequest(IReadOnlyCollection<FashionImageInput> images)
    {
        var imageIds = images.Select(i => i.ImageId).ToList();
        var idList = string.Join(", ", imageIds.Select(id => $"\"{id}\""));
        var instruction =
            $"Analyze the following {imageIds.Count} image(s) with imageIds: [{idList}]. " +
            "Identify all clearly visible people per image and extract their clothing attributes. " +
            "Use the exact imageId values in your JSON response. " +
            "Return ONLY the JSON object — no prose, no markdown, no code fences.";

        var entries = images
            .Select(i => new ChatImageEntry(i.ImageId, i.Content, i.ContentType))
            .ToList();

        return new ChatAdapterRequest(SystemPrompt, instruction, entries);
    }

    // -------------------------------------------------------------------------
    // Response parsing
    // -------------------------------------------------------------------------

    private TraitsExtractionResult ParseResponse(string raw, IReadOnlyList<string> expectedIds)
    {
        var json = raw.Trim();

        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            _logger.LogWarning("Model response contained code fences; stripping.");
            var lines = json.Split('\n');
            json = string.Join('\n',
                lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```", StringComparison.Ordinal))).Trim();
        }

        ExtractionResponseDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ExtractionResponseDto>(json, JsonOptions)
                  ?? throw new TraitsParseException("Model response deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError("JSON parse failure: {Error}", ex.Message);
            throw new TraitsParseException("Model returned malformed JSON.", ex);
        }

        // Preserve request order regardless of what order the model returned images in.
        var byId = (dto.Images ?? Enumerable.Empty<ImageExtractionDto>())
            .Where(img => img.ImageId != null)
            .ToDictionary(img => img.ImageId!, StringComparer.OrdinalIgnoreCase);

        var results = new List<ImageTraitsResult>(expectedIds.Count);
        foreach (var id in expectedIds)
        {
            if (!byId.TryGetValue(id, out var found))
            {
                _logger.LogWarning("Model omitted results for imageId '{ImageId}'.", id);
                results.Add(new ImageTraitsResult(id, Array.Empty<PersonTraits>(),
                    new[] { $"Model did not return results for image '{id}'." }));
                continue;
            }

            var people = (found.People ?? Enumerable.Empty<PersonExtractionDto>())
                .Select(MapPerson).ToList();

            results.Add(new ImageTraitsResult(
                id, people,
                found.Warnings ?? (IReadOnlyList<string>)Array.Empty<string>()));
        }

        return new TraitsExtractionResult(results);
    }

    // -------------------------------------------------------------------------
    // Mapping — DTOs → public domain types
    // -------------------------------------------------------------------------

    private static PersonTraits MapPerson(PersonExtractionDto p) => new(
        PersonId: p.PersonId ?? "person_unknown",
        Position: p.Position,
        OverallStyle: p.OverallStyle ?? (IReadOnlyList<string>)Array.Empty<string>(),
        StyleTags: p.StyleTags ?? (IReadOnlyList<string>)Array.Empty<string>(),
        ClothingItems: (p.ClothingItems ?? Enumerable.Empty<ClothingItemDto>())
            .Select(MapClothingItem).ToList(),
        OverallConfidence: p.OverallConfidence,
        FaceBoundingBox: MapBoundingBox(p.FaceBoundingBox));

    private static BoundingBox? MapBoundingBox(BoundingBoxDto? b) =>
        b is null ? null : new BoundingBox(b.X, b.Y, b.Width, b.Height);

    private static ClothingItemTraits MapClothingItem(ClothingItemDto c) => new(
        Category: c.Category ?? "unknown",
        Type: c.Type ?? "unknown",
        Subtype: c.Subtype,
        PrimaryColor: c.PrimaryColor == null
            ? null
            : new ColorTraits(c.PrimaryColor.Normalized ?? "unknown", c.PrimaryColor.Shade),
        SecondaryColors: (c.SecondaryColors ?? Enumerable.Empty<ColorDto>())
            .Select(col => new ColorTraits(col.Normalized ?? "unknown", col.Shade)).ToList(),
        Pattern: c.Pattern,
        Material: c.Material,
        Fit: c.Fit,
        Length: c.Length,
        SleeveLength: c.SleeveLength,
        Neckline: c.Neckline,
        CollarType: c.CollarType,
        WaistRise: c.WaistRise,
        ClosureType: c.ClosureType,
        Details: c.Details ?? (IReadOnlyList<string>)Array.Empty<string>(),
        VisibleText: c.VisibleText,
        Brand: c.Brand,
        Logo: c.Logo,
        Condition: c.Condition,
        StyleTags: c.StyleTags ?? (IReadOnlyList<string>)Array.Empty<string>(),
        Confidence: c.Confidence,
        BoundingBox: MapBoundingBox(c.BoundingBox));

    // -------------------------------------------------------------------------
    // GarmentTraits mapping — for ITraitsExtractor backward compatibility
    // -------------------------------------------------------------------------

    private static GarmentTraits MapToGarmentTraits(ClothingItemTraits item) => new()
    {
        Category = ParseCategory(item.Category),
        Subcategory = item.Type,
        PrimaryColor = item.PrimaryColor == null
            ? new ColorInfo("unknown", "#808080")
            : new ColorInfo(
                item.PrimaryColor.Shade ?? item.PrimaryColor.Normalized,
                ColorNameToApproxHex(item.PrimaryColor.Normalized)),
        SecondaryColors = item.SecondaryColors
            .Select(c => new ColorInfo(c.Shade ?? c.Normalized, ColorNameToApproxHex(c.Normalized)))
            .ToList(),
        Pattern = ParsePattern(item.Pattern),
        Material = item.Material,
        Fit = ParseFit(item.Fit),
        Formality = InferFormality(item.StyleTags),
        Seasonality = new List<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter },
        StyleTags = item.StyleTags.ToList(),
        Occasions = InferOccasions(item.StyleTags),
        Confidence = item.Confidence,
    };

    private static GarmentCategory ParseCategory(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "top" => GarmentCategory.Top,
            "bottom" => GarmentCategory.Bottom,
            "dress" or "one-piece" or "skirt" => GarmentCategory.Dress,
            "outerwear" => GarmentCategory.Outerwear,
            "footwear" => GarmentCategory.Footwear,
            "accessory" or "headwear" => GarmentCategory.Accessory,
            _ => GarmentCategory.Other,
        };

    private static GarmentPattern ParsePattern(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "solid" => GarmentPattern.Solid,
            "striped" => GarmentPattern.Striped,
            "plaid" => GarmentPattern.Plaid,
            "checked" => GarmentPattern.Checked,
            "floral" => GarmentPattern.Floral,
            "graphic" => GarmentPattern.Graphic,
            _ => GarmentPattern.Other,
        };

    private static GarmentFit? ParseFit(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "slim" => GarmentFit.Slim,
            "regular" => GarmentFit.Regular,
            "loose" => GarmentFit.Loose,
            "oversized" => GarmentFit.Oversized,
            _ => null,
        };

    private static int InferFormality(IReadOnlyList<string> tags)
    {
        if (tags.Any(t => t is "formal" or "evening-wear")) return 4;
        if (tags.Any(t => t is "business-casual" or "preppy")) return 3;
        return 2;
    }

    private static List<string> InferOccasions(IReadOnlyList<string> tags)
    {
        var result = new List<string>();
        if (tags.Any(t => t is "casual" or "streetwear" or "athletic" or "minimalist" or "bohemian"))
            result.Add("everyday");
        if (tags.Any(t => t is "business-casual" or "preppy"))
            result.Add("work");
        if (tags.Any(t => t is "formal" or "evening-wear"))
            result.Add("formal");
        if (tags.Any(t => t is "athletic"))
            result.Add("sport");
        if (tags.Any(t => t is "festive" or "ethnic" or "traditional"))
            result.Add("event");
        if (result.Count == 0) result.Add("everyday");
        return result;
    }

    // Representative hex values for the 16 normalized color names.
    private static string ColorNameToApproxHex(string normalized) =>
        normalized.ToLowerInvariant() switch
        {
            "black" => "#000000",
            "white" => "#ffffff",
            "gray" or "grey" => "#808080",
            "red" => "#cc0000",
            "orange" => "#ff8000",
            "yellow" => "#ffcc00",
            "green" => "#2d6a2d",
            "blue" => "#1a3a6b",
            "purple" => "#6b1a6b",
            "pink" => "#e87d9e",
            "brown" => "#7b4a2d",
            "beige" => "#f5f0dc",
            "cream" => "#fffdd0",
            "gold" => "#c8a800",
            "silver" => "#b0b0b0",
            _ => "#808080",
        };

    // -------------------------------------------------------------------------
    // System prompt
    // -------------------------------------------------------------------------

    private const string SystemPrompt = """
        You are a visual clothing and fashion attribute extraction engine.

        CRITICAL RULES:
        - Analyze ONLY clearly visible evidence. Do NOT guess or infer occluded or uncertain clothing.
        - Do NOT identify, recognize, or name any individual person.
        - Do NOT perform face recognition.
        - Do NOT infer ethnicity, religion, health status, sexual orientation, or any sensitive attribute.
        - Represent uncertainty with null or the string "unknown" — never guess.
        - Return ONLY a valid JSON object matching the schema below. No prose, no markdown, no code fences.

        PERSON IDENTIFICATION (per image):
        - Assign stable IDs within the image: person_1, person_2, etc.
        - Position: left | center | right | upper-left | upper-right | lower-left | lower-right
        - faceBoundingBox: if a face is visible, its location as normalized (0.0-1.0,
          top-left origin) { "x", "y", "width", "height" } relative to the image
          dimensions. This is LOCATION ONLY, for drawing a box on screen — never use it
          to identify, describe, or reason about who the person is. Omit (null) if no
          face is visible.

        CLOTHING ITEM EXTRACTION (for every clearly visible garment):
        category: top | bottom | dress | skirt | outerwear | footwear | headwear | accessory | one-piece | traditional-wear | unknown
        boundingBox: the garment's location as normalized (0.0-1.0, top-left origin)
          { "x", "y", "width", "height" } relative to the image dimensions. Omit (null)
          if you cannot localize it confidently.
        type: t-shirt | shirt | polo | blouse | tank-top | sweater | hoodie | cardigan | blazer | jacket |
              coat | jeans | trousers | chinos | shorts | leggings | joggers | skirt | dress | jumpsuit |
              saree | kurta | kurti | salwar | churidar | dupatta | lehenga | sherwani | dhoti | lungi |
              veshti | anarkali | palazzo | sneakers | sandals | boots | heels | loafers | cap | hat |
              turban | scarf | bag | belt | watch | glasses | jewellery — or any other precise type name.
        Indian / South Asian clothing: for sarees note body color, border color, pallu color, border width,
          zari, drape style, embroidery, print. Use "silk-like" / "cotton-like" when fabric is uncertain.
        primaryColor.normalized must be one of:
          black | white | gray | red | orange | yellow | green | blue | purple | pink |
          brown | beige | cream | gold | silver | multicolor | unknown
        pattern: solid | striped | checked | plaid | floral | geometric | polka-dot | animal-print |
          paisley | abstract | graphic | embroidered | color-blocked | tie-dye | textured | unknown
        confidence: 0.90–1.00 clearly visible; 0.75–0.89 likely; 0.50–0.74 uncertain;
          below 0.50 use null or "unknown".

        STYLE TAGS (only when supported by visible evidence):
        casual | business-casual | formal | athletic | streetwear | traditional | festive | ethnic |
        minimalist | preppy | bohemian | vintage-inspired | evening-wear

        REQUIRED OUTPUT JSON SCHEMA:
        {
          "images": [
            {
              "imageId": "<exact imageId for this image>",
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
                      "length": null,
                      "sleeveLength": "short",
                      "neckline": "round",
                      "collarType": null,
                      "waistRise": null,
                      "closureType": null,
                      "details": [],
                      "visibleText": null,
                      "brand": null,
                      "logo": null,
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
          ]
        }
        """;

    // -------------------------------------------------------------------------
    // Internal JSON deserialization DTOs — never expose outside this class
    // -------------------------------------------------------------------------

    private sealed class ExtractionResponseDto
    {
        [JsonPropertyName("images")]
        public List<ImageExtractionDto>? Images { get; set; }
    }

    private sealed class ImageExtractionDto
    {
        [JsonPropertyName("imageId")]  public string? ImageId { get; set; }
        [JsonPropertyName("people")]   public List<PersonExtractionDto>? People { get; set; }
        [JsonPropertyName("warnings")] public List<string>? Warnings { get; set; }
    }

    private sealed class PersonExtractionDto
    {
        [JsonPropertyName("personId")]         public string? PersonId { get; set; }
        [JsonPropertyName("position")]         public string? Position { get; set; }
        [JsonPropertyName("overallStyle")]     public List<string>? OverallStyle { get; set; }
        [JsonPropertyName("styleTags")]        public List<string>? StyleTags { get; set; }
        [JsonPropertyName("clothingItems")]    public List<ClothingItemDto>? ClothingItems { get; set; }
        [JsonPropertyName("overallConfidence")] public double OverallConfidence { get; set; }
        [JsonPropertyName("faceBoundingBox")]  public BoundingBoxDto? FaceBoundingBox { get; set; }
    }

    private sealed class ClothingItemDto
    {
        [JsonPropertyName("category")]      public string? Category { get; set; }
        [JsonPropertyName("type")]          public string? Type { get; set; }
        [JsonPropertyName("subtype")]       public string? Subtype { get; set; }
        [JsonPropertyName("primaryColor")]  public ColorDto? PrimaryColor { get; set; }
        [JsonPropertyName("secondaryColors")] public List<ColorDto>? SecondaryColors { get; set; }
        [JsonPropertyName("pattern")]       public string? Pattern { get; set; }
        [JsonPropertyName("material")]      public string? Material { get; set; }
        [JsonPropertyName("fit")]           public string? Fit { get; set; }
        [JsonPropertyName("length")]        public string? Length { get; set; }
        [JsonPropertyName("sleeveLength")]  public string? SleeveLength { get; set; }
        [JsonPropertyName("neckline")]      public string? Neckline { get; set; }
        [JsonPropertyName("collarType")]    public string? CollarType { get; set; }
        [JsonPropertyName("waistRise")]     public string? WaistRise { get; set; }
        [JsonPropertyName("closureType")]   public string? ClosureType { get; set; }
        [JsonPropertyName("details")]       public List<string>? Details { get; set; }
        [JsonPropertyName("visibleText")]   public string? VisibleText { get; set; }
        [JsonPropertyName("brand")]         public string? Brand { get; set; }
        [JsonPropertyName("logo")]          public string? Logo { get; set; }
        [JsonPropertyName("condition")]     public string? Condition { get; set; }
        [JsonPropertyName("styleTags")]     public List<string>? StyleTags { get; set; }
        [JsonPropertyName("confidence")]    public double Confidence { get; set; }
        [JsonPropertyName("boundingBox")]   public BoundingBoxDto? BoundingBox { get; set; }
    }

    private sealed class ColorDto
    {
        [JsonPropertyName("normalized")] public string? Normalized { get; set; }
        [JsonPropertyName("shade")]      public string? Shade { get; set; }
    }

    private sealed class BoundingBoxDto
    {
        [JsonPropertyName("x")]      public double X { get; set; }
        [JsonPropertyName("y")]      public double Y { get; set; }
        [JsonPropertyName("width")]  public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
    }
}
