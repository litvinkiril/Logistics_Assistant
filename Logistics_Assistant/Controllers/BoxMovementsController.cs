using System.Security.Claims;
using Logistics_Assistant.DTOs.BoxMovements;
using Logistics_Assistant.Services.BoxMovements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers;

[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Route("api/delivery-orders/{orderId:long}/boxes")]
public sealed class BoxMovementsController(BoxMovementService movements) : ControllerBase
{
    [HttpPost("load")]
    public async Task<IActionResult> LoadAsync(
        long orderId,
        [FromBody] LoadBoxRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("user_id");
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Не удалось определить пользователя.");

        var result = await movements.LoadAsync(orderId, userId, request, cancellationToken);

        return result switch
        {
            LoadBoxResult.Loaded => NoContent(),
            LoadBoxResult.InvalidRequest => BadRequest("Укажите позицию рейса и только одно: ID коробки или её URL."),
            LoadBoxResult.OrderNotFound => NotFound("Рейс не найден."),
            LoadBoxResult.ItemNotFound => NotFound("Позиция рейса не найдена."),
            LoadBoxResult.BoxNotFound => NotFound("Коробка не найдена."),
            LoadBoxResult.NotResponsible => StatusCode(StatusCodes.Status403Forbidden, "Вы не ответственный за этот рейс."),
            LoadBoxResult.InvalidOrderStatus => Conflict("Рейс должен находиться на погрузке."),
            LoadBoxResult.InvalidSource => Conflict("У рейса должен быть указан активный склад погрузки."),
            LoadBoxResult.BoxUnavailable => Conflict("Коробка недоступна на складе рейса или не соответствует позиции плана."),
            LoadBoxResult.Conflict => Conflict("Данные изменились. Обновите их и повторите сканирование."),
            _ => throw new InvalidOperationException("Неизвестный результат погрузки.")
        };
    }
}
