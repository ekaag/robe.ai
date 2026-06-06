using Microsoft.AspNetCore.Mvc;

namespace Robe.Api.Controllers;

[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> Get() => "All is well";
}
