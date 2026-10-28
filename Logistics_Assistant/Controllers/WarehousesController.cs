using Logistics_Assistant.Services.Warehouses;
using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers
{
    [ApiController]
    [Route("api/warehouses")]
    public class WarehousesController : ControllerBase
    {
        private readonly WarehouseStockService _warehouses;

        public WarehousesController(WarehouseStockService warehouses)
        {
            _warehouses = warehouses;
        }

        [HttpGet]
        public async Task<IActionResult> GetWarehousesAsync(
            CancellationToken cancellationToken)
        {
            var warehouses = await _warehouses.GetWarehousesAsync(
                cancellationToken);

            return Ok(warehouses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetByIdAsync(
    long id,
    CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return BadRequest("ID должен быть положительным числом.");
            }

            var warehouse = await _warehouses.GetWarehouseByIdAsync(
                id,
                cancellationToken);

            if (warehouse is null)
            {
                return NotFound("Склад не найден.");
            }

            return Ok(warehouse);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchByNameAsync(
            [FromQuery] string? name,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Укажи название склада.");
            }

            var warehouses = await _warehouses.SearchWarehousesByNameAsync(
                name,
                cancellationToken);

            return Ok(warehouses);
        }
    }
}