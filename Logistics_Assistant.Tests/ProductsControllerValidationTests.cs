using Logistics_Assistant.Controllers;
using Logistics_Assistant.Data;
using Logistics_Assistant.Services.LogisticsPoints;
using Logistics_Assistant.Services.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Logistics_Assistant.Tests;

public class ProductsControllerValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public async Task InvalidProductIdsReturnBadRequest(long id)
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);
        Assert.IsType<BadRequestObjectResult>(await controller.GetByIdAsync(id, default));
        Assert.IsType<BadRequestObjectResult>(await controller.GetStockAsync(id, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNameReturnsBadRequest(string? name)
    {
        using var db = CreateDbContext();
        var result = await CreateController(db).SearchByNameAsync(name);
        Assert.Equal("Укажи название товара.", Assert.IsType<BadRequestObjectResult>(result).Value);
    }

    [Fact]
    public async Task NegativeOffsetReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var result = await CreateController(db).SearchByNameAsync("болт", -1);
        Assert.Equal("Смещение должно быть целым неотрицательным числом.",
            Assert.IsType<BadRequestObjectResult>(result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyUrlReturnsBadRequest(string? url)
    {
        using var db = CreateDbContext();
        var result = await CreateController(db).SearchByUrlAsync(url, default);
        Assert.Equal("Укажи URL товара.", Assert.IsType<BadRequestObjectResult>(result).Value);
    }

    private static ProductsController CreateController(ApplicationDbContext db) =>
        new(new ProductSearchService(db), new LogisticsPointService(db));

    private static ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
}
