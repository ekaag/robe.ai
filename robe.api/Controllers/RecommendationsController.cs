using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Api.Models;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

/// <summary>
/// Style-matched clothing recommendations.
/// </summary>
[ApiController]
[Route("api/users/me/recommendations")]
[Authorize]
[Produces("application/json")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommender _recommender;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IProfileRepository _profileRepo;
    private readonly ICurrentUser _currentUser;

    public RecommendationsController(
        IRecommender recommender,
        IInventoryRepository inventoryRepo,
        IProfileRepository profileRepo,
        ICurrentUser currentUser)
    {
        _recommender   = recommender;
        _inventoryRepo = inventoryRepo;
        _profileRepo   = profileRepo;
        _currentUser   = currentUser;
    }

    /// <summary>
    /// Get ranked recommendations based on the user's style profile and inventory.
    /// </summary>
    /// <remarks>
    /// Requires a generated style profile. Returns items ranked by fit score,
    /// filtered by optional budget, category, and occasion constraints.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(RecommendationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recommend([FromBody] RecommendRequest request, CancellationToken ct)
    {
        if (request.Count is < 1 or > 20)
            return BadRequest(new ErrorResponse("count must be between 1 and 20."));

        if (request.MaxBudget is < 0)
            return BadRequest(new ErrorResponse("maxBudget must be non-negative."));

        var userId  = _currentUser.UserId;
        var profile = await _profileRepo.GetAsync(userId, ct);
        if (profile is null)
            return NotFound(new ErrorResponse("No style profile found. Run POST /api/users/me/profile/generate first."));

        var context = new RecommendationContext
        {
            MaxBudget  = request.MaxBudget,
            Currency   = request.Currency ?? "USD",
            Categories = request.Categories ?? new List<GarmentCategory>(),
            Occasion   = request.Occasion,
            Count      = request.Count
        };

        var query = new InventoryQuery(
            Categories: context.Categories.Count > 0 ? context.Categories : null,
            Occasion:   context.Occasion,
            MaxPrice:   context.MaxBudget,
            Currency:   context.Currency,
            InStockOnly: true);

        var candidates      = await _inventoryRepo.QueryAsync(query, ct);
        var recommendations = await _recommender.RecommendAsync(profile, candidates, context, ct);

        return Ok(new RecommendationsResponse(
            recommendations,
            profile.ModifiedAt,
            DateTimeOffset.UtcNow));
    }
}

public record RecommendRequest(
    decimal? MaxBudget = null,
    string? Currency = null,
    List<GarmentCategory>? Categories = null,
    string? Occasion = null,
    int Count = 5);
