using Microsoft.AspNetCore.Mvc;

namespace Robe.Api.Controllers;

/// <summary>
/// Health check.
/// </summary>
[ApiController]
[Route("")]
[Produces("application/json")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// Simple liveness check — returns 200 if the API is running.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult<string> Get() => "All is well";
}
