using Logistics_Assistant.Data;
using Logistics_Assistant.Pages.Products;
using Logistics_Assistant.Services.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Logistics_Assistant.Services.LogisticsPoints;
using Xunit;

namespace Logistics_Assistant.Tests;

public class SearchModelValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("9223372036854775808")]
    public async Task ById_InvalidId_ReturnsBadRequest(string? id)
    {
        // Arrange: подготавливаем зависимости и обработчик.
        using var db = CreateDbContext();
        var service = new ProductSearchService(db);
        var page = new SearchModel(service);

        // Act: вызываем обработчик.
        var result = await page.OnGetByIdAsync(id);

        // Assert: проверяем ответ.
        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("ID должен быть числом.", error.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ByName_EmptyName_ReturnsBadRequest(string? name)
    {
        using var db = CreateDbContext();
        var service = new ProductSearchService(db);
        var page = new SearchModel(service);

        var result = await page.OnGetByNameAsync(name, "0");

        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Укажи название товара.", error.Value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task ByName_InvalidOffset_ReturnsBadRequest(string offset)
    {
        using var db = CreateDbContext();
        var service = new ProductSearchService(db);
        var page = new SearchModel(service);

        var result = await page.OnGetByNameAsync("болт", offset);

        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Смещение должно быть целым неотрицательным числом.",
            error.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ByUrl_EmptyUrl_ReturnsBadRequest(string? url)
    {
        using var db = CreateDbContext();
        var service = new ProductSearchService(db);
        var page = new SearchModel(service);

        var result = await page.OnGetByUrlAsync(url);

        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Укажи URL товара.", error.Value);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .Options;

        return new ApplicationDbContext(options);
    }
    [Fact]
    public async Task WarehouseStock_TextId_ReturnsBadRequest()
    {
        using var db = CreateDbContext();

        var page = new SearchModel(new ProductSearchService(db));
        var stockService = new LogisticsPointService(db);

        var result = await page.OnGetWarehouseStockByProductIdAsync(
            "abc",
            stockService);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
