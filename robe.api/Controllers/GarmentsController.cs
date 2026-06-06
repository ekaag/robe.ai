using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

[ApiController]
[Route("api/garments")]
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

    private readonly ITraitsExtractor _extractor;
    private readonly IGarmentRepository _repository;
    private readonly IImageStore _imageStore;
    private readonly ICurrentUser _currentUser;

    public GarmentsController(
        ITraitsExtractor extractor,
        IGarmentRepository repository,
        IImageStore imageStore,
        ICurrentUser currentUser)
    {
        _extractor   = extractor;
        _repository  = repository;
        _imageStore  = imageStore;
        _currentUser = currentUser;
    }

    // ── API #1 ──────────────────────────────────────────────────────────────

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "imageBase64 is required." });

        byte[] imageBytes;
        try { imageBytes = Convert.FromBase64String(request.ImageBase64); }
        catch (FormatException) { return BadRequest(new { error = "imageBase64 is not valid base64." }); }

        if (imageBytes.Length > MaxImageBytes)
            return BadRequest(new { error = "Image exceeds the 10 MB limit." });

        if (request.MimeType is null || !AllowedMimeTypes.Contains(request.MimeType))
            return BadRequest(new { error = $"Unsupported mimeType '{request.MimeType}'. Allowed: image/jpeg, image/png, image/webp." });

        try
        {
            var traits = await _extractor.ExtractAsync(new ImageInput(imageBytes, request.MimeType), ct);
            return Ok(new { traits, modelVersion = ModelVersion });
        }
        catch (GarmentNotDetectedException)
        {
            return UnprocessableEntity(new { error = "No garment detected in the provided image." });
        }
        catch (TraitsParseException ex)
        {
            return StatusCode(502, new { error = "Model returned an invalid response.", detail = ex.Message });
        }
        catch (ExtractionException ex)
        {
            return StatusCode(502, new { error = "Model service call failed.", detail = ex.Message });
        }
    }

    // ── API #2 ──────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateGarmentRequest request, CancellationToken ct)
    {
        if (request.Traits is null)
            return BadRequest(new { error = "traits is required." });

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "imageBase64 is required." });

        byte[] imageBytes;
        try { imageBytes = Convert.FromBase64String(request.ImageBase64); }
        catch (FormatException) { return BadRequest(new { error = "imageBase64 is not valid base64." }); }

        if (imageBytes.Length > MaxImageBytes)
            return BadRequest(new { error = "Image exceeds the 10 MB limit." });

        if (request.MimeType is null || !AllowedMimeTypes.Contains(request.MimeType))
            return BadRequest(new { error = $"Unsupported mimeType '{request.MimeType}'. Allowed: image/jpeg, image/png, image/webp." });

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

    [HttpGet]
    [Authorize]
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

        return Ok(new { items = items.Select(ToResponse), page, pageSize });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var garment = await _repository.GetByIdAsync(id, _currentUser.UserId, ct);
        return garment is null ? NotFound() : Ok(ToResponse(garment));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var garment = await _repository.GetByIdAsync(id, _currentUser.UserId, ct);
        if (garment is null) return NotFound();

        await _imageStore.DeleteAsync(garment.BlobKey, ct);
        await _repository.DeleteAsync(id, _currentUser.UserId, ct);

        return NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static object ToResponse(Garment g) => new
    {
        g.Id,
        g.UserId,
        g.Traits,
        g.ImageUrl,
        g.CreatedAt,
        g.ModifiedAt,
        g.CreatedByUserId,
        g.ModifiedByUserId
    };
}

public record AnalyzeRequest(string? ImageBase64, string? MimeType);
public record CreateGarmentRequest(GarmentTraits? Traits, string? ImageBase64, string? MimeType);
