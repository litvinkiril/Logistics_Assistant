using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers;

[ApiController]
public class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index() => Ok(new
    {
        name = "Logistics Assistant API",
        logisticsPoints = "/api/logistics-points",
        productSearch = "/api/products/search?name=шкаф"
    });
}
