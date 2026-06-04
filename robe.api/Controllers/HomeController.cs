using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    [Route("")]
    [Route("Home")]
    [Route("Home/Index")]
    [Route("Home/Index/{id?}")]
    // public IActionResult Index(int? id)
    // {
    //     return ControllerContext.MyDisplayRouteInfo(id);
    // }

    // [Route("Home/About")]
    // [Route("Home/About/{id?}")]
    // public IActionResult About(int? id)
    // {
    //     return ControllerContext.;
    // }

    public IActionResult Index()
    {
        // ... some code ...
        return View();
    }

    public string MyDisplayRouteInfo()
    {
        // Logic to display route information
        return "Route Info: " + ControllerContext.RouteData.Values["controller"] + "/" + ControllerContext.RouteData.Values["action"];
    }
}