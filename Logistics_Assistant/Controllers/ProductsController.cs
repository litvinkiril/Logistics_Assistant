using Logistics_Assistant.Services.LogisticsPoints;
using Logistics_Assistant.Services.Products;
using Microsoft.AspNetCore.Mvc;

namespace Logistics_Assistant.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductSearchService _search;
    private readonly LogisticsPointService _points;

    public ProductsController(ProductSearchService search, LogisticsPointService points)
    {
        _search = search;
        _points = points;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("ID должен быть положительным целым числом.");

        var products = await _search.SearchByIdAsync(id, cancellationToken);
        if (products.Count == 0)
            return NotFound("Товар не найден.");

        // Keep the previous search response shape: an array with one product.
        return Ok(products);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchByNameAsync(
        [FromQuery] string? name,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Укажи название товара.");
        if (offset < 0)
            return BadRequest("Смещение должно быть целым неотрицательным числом.");

        var products = await _search.SearchByNameAsync(name, offset, cancellationToken);
        return Ok(products);
    }

    [HttpGet("by-url")]
    public async Task<IActionResult> SearchByUrlAsync(
        [FromQuery] string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("Укажи URL товара.");

        var products = await _search.SearchByUrlAsync(url, cancellationToken);
        return Ok(products);
    }

    [HttpGet("{id:long}/stock")]
    public async Task<IActionResult> GetStockAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("ID должен быть положительным целым числом.");

        var result = await _points.SearchProductStockByIdAsync(id, cancellationToken);
        return result is null ? NotFound("Товар не найден.") : Ok(result);
    }

    [HttpGet("{productId:long}/stock/{pointId:long}")]
    public async Task<IActionResult> GetStockAtPointAsync(
    long productId,
    long pointId,
    CancellationToken cancellationToken)
    {
        if (productId <= 0 || pointId <= 0)
            return BadRequest("ID товара и точки должны быть положительными числами.");

        var result = await _search.SearchProductStockAtPointAsync(
            productId,
            pointId,
            cancellationToken);

        return result is null
            ? NotFound("Товар или логистическая точка не найдены.")
            : Ok(result);
    }
}
