using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Api.Models;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

/// <summary>
/// Garment analysis, storage, and retrieval.
/// </summary>
[ApiController]
[Route("api/garments")]
[Produces("application/json")]
public class GarmentsController : ControllerBase
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
    private const string ModelVersion = "azure-openai-gpt-4o";

    private static readonly Dictionary<string, string> MimeToExtension = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"]  = ".png",
        ["image/webp"] = ".webp"
    };

    private const int MaxImagesPerBatch = 10;

    private readonly ITraitsExtractor _extractor;
    private readonly IFashionTraitsExtractor _fashionExtractor;
    private readonly IGarmentRepository _repository;
    private readonly IImageStore _imageStore;
    private readonly ICurrentUser _currentUser;

    public GarmentsController(
        ITraitsExtractor extractor,
        IFashionTraitsExtractor fashionExtractor,
        IGarmentRepository repository,
        IImageStore imageStore,
        ICurrentUser currentUser)
    {
        _extractor        = extractor;
        _fashionExtractor = fashionExtractor;
        _repository       = repository;
        _imageStore       = imageStore;
        _currentUser      = currentUser;
    }

    // ── API #1 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Analyze a garment image and extract structured traits.
    /// </summary>
    /// <remarks>
    /// Stateless — sends the image to a vision model and returns extracted traits.
    /// No authentication required. No data is persisted.
    /// </remarks>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), 502)]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new ErrorResponse("imageBase64 is required."));

        byte[] imageBytes;
        try { imageBytes = Convert.FromBase64String(request.ImageBase64); }
        catch (FormatException) { return BadRequest(new ErrorResponse("imageBase64 is not valid base64.")); }

        if (imageBytes.Length > MaxImageBytes)
            return BadRequest(new ErrorResponse("Image exceeds the 10 MB limit."));

        if (request.MimeType is null || !AllowedMimeTypes.Contains(request.MimeType))
            return BadRequest(new ErrorResponse($"Unsupported mimeType '{request.MimeType}'. Allowed: image/jpeg, image/png, image/webp."));

        try
        {
            var traits = await _extractor.ExtractAsync(new ImageInput(imageBytes, request.MimeType), ct);
            return Ok(new AnalyzeResponse(traits, ModelVersion));
        }
        catch (GarmentNotDetectedException)
        {
            return UnprocessableEntity(new ErrorResponse("No garment detected in the provided image."));
        }
        catch (TraitsParseException ex)
        {
            return StatusCode(502, new ErrorResponse("Model returned an invalid response.", ex.Message));
        }
        catch (ExtractionException ex)
        {
            return StatusCode(502, new ErrorResponse("Model service call failed.", ex.Message));
        }
    }

    /// <summary>
    /// Analyze multiple garment images and extract per-person clothing traits from each.
    /// </summary>
    /// <remarks>
    /// Stateless. Sends all images to the vision model in one call and returns structured
    /// traits per person per image. Captures multi-person outfits that /analyze cannot.
    /// No authentication required. No data is persisted.
    /// </remarks>
    [HttpPost("analyze-batch")]
    [ProducesResponseType(typeof(BatchAnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), 502)]
    public async Task<IActionResult> AnalyzeBatch([FromBody] BatchAnalyzeRequest request, CancellationToken ct)
    {
        if (request.Images is null || request.Images.Count == 0)
            return BadRequest(new ErrorResponse("At least one image is required."));

        if (request.Images.Count > MaxImagesPerBatch)
            return BadRequest(new ErrorResponse($"A maximum of {MaxImagesPerBatch} images are allowed per request."));

        var inputs = new List<FashionImageInput>();
        for (int i = 0; i < request.Images.Count; i++)
        {
            var img     = request.Images[i];
            var imageId = string.IsNullOrWhiteSpace(img.ImageId) ? $"img-{i + 1}" : img.ImageId;

            if (string.IsNullOrWhiteSpace(img.ImageBase64))
                return BadRequest(new ErrorResponse($"Image '{imageId}': imageBase64 is required."));

            byte[] bytes;
            try { bytes = Convert.FromBase64String(img.ImageBase64); }
            catch (FormatException) { return BadRequest(new ErrorResponse($"Image '{imageId}': imageBase64 is not valid base64.")); }

            if (bytes.Length > MaxImageBytes)
                return BadRequest(new ErrorResponse($"Image '{imageId}': exceeds the 10 MB limit."));

            if (img.MimeType is null || !AllowedMimeTypes.Contains(img.MimeType))
                return BadRequest(new ErrorResponse($"Image '{imageId}': unsupported mimeType '{img.MimeType}'. Allowed: image/jpeg, image/png, image/webp."));

            inputs.Add(new FashionImageInput(imageId, bytes, img.MimeType));
        }

        try
        {
            var result = await _fashionExtractor.ExtractTraitsAsync(inputs, ct);
            return Ok(new BatchAnalyzeResponse(result.Images, ModelVersion));
        }
        catch (ContentFilterException ex)
        {
            return StatusCode(502, new ErrorResponse("Content was filtered by the model.", ex.Message));
        }
        catch (TraitsParseException ex)
        {
            return StatusCode(502, new ErrorResponse("Model returned an invalid response.", ex.Message));
        }
        catch (ExtractionException ex)
        {
            return StatusCode(502, new ErrorResponse("Model service call failed.", ex.Message));
        }
    }

    // ── API #2 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Store a new garment with its image and extracted traits.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(GarmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateGarmentRequest request, CancellationToken ct)
    {
        if (request.Traits is null)
            return BadRequest(new ErrorResponse("traits is required."));

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new ErrorResponse("imageBase64 is required."));

        byte[] imageBytes;
        try { imageBytes = Convert.FromBase64String(request.ImageBase64); }
        catch (FormatException) { return BadRequest(new ErrorResponse("imageBase64 is not valid base64.")); }

        if (imageBytes.Length > MaxImageBytes)
            return BadRequest(new ErrorResponse("Image exceeds the 10 MB limit."));

        if (request.MimeType is null || !AllowedMimeTypes.Contains(request.MimeType))
            return BadRequest(new ErrorResponse($"Unsupported mimeType '{request.MimeType}'. Allowed: image/jpeg, image/png, image/webp."));

        var id      = $"grm_{Guid.NewGuid():N}";
        var ext     = MimeToExtension[request.MimeType];
        var blobKey = $"{id}{ext}";
        var userId  = _currentUser.UserId;
        var now     = DateTimeOffset.UtcNow;

        var imageUrl = await _imageStore.SaveAsync(new ImageInput(imageBytes, request.MimeType), blobKey, ct);

        var garment = new Garment
        {
            Id              = id,
            UserId          = userId,
            Traits          = request.Traits,
            ImageUrl        = imageUrl,
            BlobKey         = blobKey,
            CreatedAt       = now,
            ModifiedAt      = now,
            CreatedByUserId = userId,
            ModifiedByUserId = userId
        };

        var saved = await _repository.AddAsync(garment, ct);
        return CreatedAtAction(nameof(GetById), new { id = saved.Id }, ToResponse(saved));
    }

    /// <summary>
    /// List the current user's garments with optional category filter and paging.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(GarmentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] GarmentCategory? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var query = new GarmentQuery(category, page, pageSize);
        var items = await _repository.ListAsync(_currentUser.UserId, query, ct);

        return Ok(new GarmentListResponse(items.Select(ToResponse), page, pageSize));
    }

    /// <summary>
    /// Get a single garment by ID (must belong to the current user).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(GarmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var garment = await _repository.GetByIdAsync(id, _currentUser.UserId, ct);
        return garment is null ? NotFound() : Ok(ToResponse(garment));
    }

    /// <summary>
    /// Delete a garment and its stored image (must belong to the current user).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var garment = await _repository.GetByIdAsync(id, _currentUser.UserId, ct);
        if (garment is null) return NotFound();

        await _imageStore.DeleteAsync(garment.BlobKey, ct);
        await _repository.DeleteAsync(id, _currentUser.UserId, ct);

        return NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GarmentResponse ToResponse(Garment g) => new(
        g.Id,
        g.UserId,
        g.Traits,
        g.ImageUrl,
        g.CreatedAt,
        g.ModifiedAt,
        g.CreatedByUserId,
        g.ModifiedByUserId);
}

public record AnalyzeRequest(string? ImageBase64, string? MimeType);
public record CreateGarmentRequest(GarmentTraits? Traits, string? ImageBase64, string? MimeType);
public record BatchAnalyzeImageRequest(string? ImageId, string? ImageBase64, string? MimeType);
public record BatchAnalyzeRequest(IReadOnlyList<BatchAnalyzeImageRequest>? Images);
