using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Api.Models;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

/// <summary>
/// Style profile generation and retrieval.
/// </summary>
[ApiController]
[Route("api/users/me/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IProfileGenerator _generator;
    private readonly IProfileRepository _profileRepo;
    private readonly IGarmentRepository _garmentRepo;
    private readonly ICurrentUser _currentUser;

    public ProfileController(
        IProfileGenerator generator,
        IProfileRepository profileRepo,
        IGarmentRepository garmentRepo,
        ICurrentUser currentUser)
    {
        _generator   = generator;
        _profileRepo = profileRepo;
        _garmentRepo = garmentRepo;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Generate or refresh the current user's style profile from their wardrobe.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var now    = DateTimeOffset.UtcNow;

        var garments = await _garmentRepo.ListAsync(userId, new GarmentQuery(null, Page: 1, PageSize: int.MaxValue), ct);
        var traits   = garments.Select(g => g.Traits).ToList();

        var existing = await _profileRepo.GetAsync(userId, ct);

        var profile = await _generator.GenerateAsync(traits, ct);

        var toSave = new StyleProfile
        {
            DominantStyles   = profile.DominantStyles,
            ColorPalette     = profile.ColorPalette,
            FormalityRange   = profile.FormalityRange,
            PreferredFits    = profile.PreferredFits,
            SeasonalSkew     = profile.SeasonalSkew,
            Summary          = profile.Summary,
            GarmentCount     = profile.GarmentCount,
            CreatedAt        = existing?.CreatedAt ?? now,
            CreatedByUserId  = existing?.CreatedByUserId ?? userId,
            ModifiedAt       = now,
            ModifiedByUserId = userId
        };

        await _profileRepo.SaveAsync(userId, toSave, ct);
        return Ok(ToResponse(toSave));
    }

    /// <summary>
    /// Get the current user's last generated style profile.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var profile = await _profileRepo.GetAsync(_currentUser.UserId, ct);
        return profile is null ? NotFound() : Ok(ToResponse(profile));
    }

    private static ProfileResponse ToResponse(StyleProfile p) => new(
        p.DominantStyles,
        p.ColorPalette,
        p.FormalityRange,
        p.PreferredFits,
        p.SeasonalSkew,
        p.Summary,
        p.GarmentCount,
        p.CreatedAt,
        p.ModifiedAt,
        p.CreatedByUserId,
        p.ModifiedByUserId);
}
