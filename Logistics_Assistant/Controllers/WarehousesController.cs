using Logistics_Assistant.Services.LogisticsPoints;
using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers
{
    [ApiController]
    [Route("api/logistics-points")]
    public class LogisticsPointsController : ControllerBase
    {
        private readonly LogisticsPointService _points;

        public LogisticsPointsController(LogisticsPointService points)
        {
            _points = points;
        }

        [HttpGet]
        public async Task<IActionResult> GetPointsAsync(
            [FromQuery] string? type,
            CancellationToken cancellationToken)
        {
            var points = await _points.GetPointsAsync(
                type,
                cancellationToken);

            return Ok(points);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return BadRequest(
                    "ID должен быть положительным числом.");
            }

            var point = await _points.GetPointByIdAsync(
                id,
                cancellationToken);

            if (point is null)
            {
                return NotFound("Точка не найдена.");
            }

            return Ok(point);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchAsync(
            [FromQuery] string? query,
            [FromQuery] string? type,
            CancellationToken cancellationToken)
        {
            var points = await _points.SearchPointsAsync(
                query,
                type,
                cancellationToken);

            return Ok(points);
        }
    }
}