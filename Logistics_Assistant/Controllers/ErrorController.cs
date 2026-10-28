using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : ControllerBase
{
    // No HTTP verb restriction: exception handling can re-execute POST as well as GET.
    [Route("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HandleError() => Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Не удалось обработать запрос.");
}
