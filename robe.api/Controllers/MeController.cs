using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Robe.Core.Interfaces;

namespace Robe.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public MeController(ICurrentUser currentUser) => _currentUser = currentUser;

    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Get() =>
        Ok(new MeResponse(_currentUser.UserId, _currentUser.Name, _currentUser.Provider));
}

public record MeResponse(string UserId, string? Name, string? Provider);
