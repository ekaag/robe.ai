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

    private readonly ITraitsExtractor _extractor;

    public GarmentsController(ITraitsExtractor extractor) => _extractor = extractor;

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "imageBase64 is required." });

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(request.ImageBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "imageBase64 is not valid base64." });
        }

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
}

public record AnalyzeRequest(string? ImageBase64, string? MimeType);
