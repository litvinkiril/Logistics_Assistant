using Logistics_Assistant.Models;
using Logistics_Assistant.Services.DeliveryOrders;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Logistics_Assistant.DTOs.DeliveryOrders;

namespace Logistics_Assistant.Controllers;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("api/delivery-orders")]
public class DeliveryOrdersController : ControllerBase
{
    private readonly DeliveryOrderService _orders;

    public DeliveryOrdersController(DeliveryOrderService orders)
    {
        _orders = orders;
    }

    [HttpGet("unassigned")]
    public async Task<ActionResult<List<DeliveryOrder>>> GetUnassignedAsync(
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            return BadRequest("Смещение должно быть неотрицательным числом.");

        var orders = await _orders.GetUnassignedAsync(
            offset, cancellationToken);

        return Ok(orders);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<DeliveryOrder>>> GetMineAsync(
    CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Не удалось определить пользователя.");

        var orders = await _orders.GetByUserIdAsync(
            userId,
            cancellationToken);

        return Ok(orders);
    }

    [Authorize]
    [HttpPatch("{id:long}/assign")]
    public async Task<IActionResult> AssignResponsibleAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("ID доставки должен быть положительным числом.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Не удалось определить пользователя.");

        var result = await _orders.AssignResponsibleAsync(
            id,
            userId,
            cancellationToken);

        return result switch
        {
            AssignResponsibleResult.Assigned =>
                NoContent(),

            AssignResponsibleResult.OrderNotFound =>
                NotFound("Доставка не найдена."),

            AssignResponsibleResult.UserNotFound =>
                Unauthorized("Пользователь не найден."),

            AssignResponsibleResult.AlreadyAssigned =>
                Conflict("У доставки уже назначен ответственный."),

            _ => throw new InvalidOperationException(
                "Неизвестный результат назначения.")
        };
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DeliveryOrderDetailsResponse>> GetDetailsAsync(
    long id,
    CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("ID рейса должен быть положительным числом.");

        var result = await _orders.GetDetailsAsync(
            id,
            cancellationToken);

        if (result is null)
            return NotFound("Рейс не найден.");

        return Ok(result);
    }

}
