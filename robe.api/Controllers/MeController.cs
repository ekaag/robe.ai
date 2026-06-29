using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

/// <summary>
/// Auth verification — confirms a token is valid and returns the caller's identity.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
[Produces("application/json")]
public class MeController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public MeController(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>
    /// Return the current user's identity derived from the validated JWT.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Get() =>
        Ok(new MeResponse(_currentUser.UserId, _currentUser.Name, _currentUser.Provider));
}

public record MeResponse(string UserId, string? Name, string? Provider);
