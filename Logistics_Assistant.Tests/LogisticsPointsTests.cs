using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Controllers;
using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Logistics_Assistant.Services.LogisticsPoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "LogisticsPoints")]
public sealed class LogisticsPointsValidationTests : IDisposable
{
    private readonly ServiceProvider _cacheServices = TestCache.CreateServices();

    public void Dispose() => _cacheServices.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public async Task ControllerRejectsNonpositiveIdWithoutQueryingDatabase(long id)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
        var controller = new LogisticsPointsController(new LogisticsPointService(
            db, _cacheServices.GetRequiredService<HybridCache>()));
        var result = await controller.GetByIdAsync(id, CancellationToken.None);
        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("ID должен быть положительным числом.", error.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ServiceRejectsNonpositiveId(long id)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
        var service = new LogisticsPointService(db, _cacheServices.GetRequiredService<HybridCache>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPointByIdAsync(id, CancellationToken.None));
    }
}

[Trait("Feature", "LogisticsPoints")]
public sealed class LogisticsPointsIntegrationTests : IClassFixture<LogisticsPointsFixture>
{
    private readonly LogisticsPointsFixture _fixture;
    public LogisticsPointsIntegrationTests(LogisticsPointsFixture fixture) => _fixture = fixture;

    [PointsPostgresTheory]
    [InlineData(null, 8)]
    [InlineData("", 8)]
    [InlineData("  ", 8)]
    [InlineData("warehouse", 4)]
    [InlineData("pvz", 4)]
    [InlineData(" warehouse ", 4)]
    [InlineData("unknown", 0)]
    [InlineData("WAREHOUSE", 0)]
    public async Task ListFiltersByExactTrimmedType(string? type, int count)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        var points = await service.GetPointsAsync(type, CancellationToken.None);
        Assert.Equal(count, points.Count);
        if (!string.IsNullOrWhiteSpace(type)) Assert.All(points, point => Assert.Equal(type.Trim(), point.Type));
    }

    [PointsPostgresTheory]
    [InlineData("сЕвЕрНый", new long[] { 1 })]
    [InlineData(" мОсКвА ", new long[] { 1, 2, 8 })]
    [InlineData("аЛаБяНа", new long[] { 2, 8 })]
    [InlineData("%", new long[] { 5 })]
    [InlineData("_", new long[] { 6 })]
    [InlineData("\\", new long[] { 7 })]
    [InlineData("несуществующая точка", new long[] { })]
    [InlineData("' OR 1=1 --", new long[] { })]
    public async Task SearchMatchesNameCityAddressAndTreatsWildcardsLiterally(string query, long[] expectedIds)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        var points = await service.SearchPointsAsync(query, null, CancellationToken.None);
        Assert.Equal(expectedIds, points.Select(point => point.Id).Order().ToArray());
    }

    [PointsPostgresFact]
    public async Task BlankSearchReturnsAllAndEqualNamesAreOrderedById()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        Assert.Equal(8, (await service.SearchPointsAsync("  ", null, CancellationToken.None)).Count);
        var points = await service.SearchPointsAsync("Сокол", "pvz", CancellationToken.None);
        Assert.Equal(new long[] { 2, 8 }, points.Select(point => point.Id));
        Assert.True(points[0].IsActive);
        Assert.False(points[1].IsActive); // Current directory includes inactive points.
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ChangeTracker.Entries());
    }

    [PointsPostgresFact]
    public async Task SearchCombinesTextAndTypeFilters()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        var point = Assert.Single(await service.SearchPointsAsync("Москва", "warehouse", CancellationToken.None));
        Assert.Equal(1, point.Id);
    }

    [PointsPostgresFact]
    public async Task CancelledRequestDoesNotExecuteSearch()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetPointsAsync(null, cancellation.Token));
    }

    [PointsPostgresTheory]
    [InlineData(1, "warehouse")]
    [InlineData(2, "pvz")]
    [InlineData(3, "warehouse")]
    public async Task ApiReturnsPointDetailsForBothTypesIncludingInactive(long id, string type)
    {
        using var response = await _fixture.Client.GetAsync($"/api/logistics-points/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var point = await response.Content.ReadFromJsonAsync<LogisticsPoint>();
        Assert.NotNull(point);
        Assert.Equal(id, point.Id);
        Assert.Equal(type, point.Type);
        Assert.False(string.IsNullOrWhiteSpace(point.Address));
        Assert.Equal(id != 3, point.IsActive);
    }

    [PointsPostgresTheory]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("-1", HttpStatusCode.BadRequest)]
    [InlineData("999999", HttpStatusCode.NotFound)]
    [InlineData("not-an-id", HttpStatusCode.NotFound)]
    [InlineData("9223372036854775808", HttpStatusCode.NotFound)]
    public async Task ApiReturnsExpectedErrorsForInvalidOrMissingIds(string id, HttpStatusCode expected)
    {
        using var response = await _fixture.Client.GetAsync($"/api/logistics-points/{id}");
        Assert.Equal(expected, response.StatusCode);
        if (id == "999999") Assert.Equal("Точка не найдена.", await response.Content.ReadAsStringAsync());
    }

    [PointsPostgresTheory]
    [InlineData("/api/logistics-points", 8)]
    [InlineData("/api/logistics-points?type=pvz", 4)]
    [InlineData("/api/logistics-points?type=warehouse", 4)]
    [InlineData("/api/logistics-points/search", 8)]
    [InlineData("/api/logistics-points/search?query=%D0%9C%D0%BE%D1%81%D0%BA%D0%B2%D0%B0&type=pvz", 2)]
    [InlineData("/api/logistics-points/search?query=%25", 1)]
    [InlineData("/api/logistics-points/search?type=unknown", 0)]
    public async Task ApiListAndSearchAreRegisteredAndReturnJsonArrays(string url, int expectedCount)
    {
        using var response = await _fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var points = await response.Content.ReadFromJsonAsync<List<LogisticsPoint>>();
        Assert.NotNull(points);
        Assert.Equal(expectedCount, points.Count);
    }

    [PointsPostgresFact]
    public async Task StockLookupIncludesWarehouseAndPickupPointAndPreservesQuantities()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LogisticsPointService>();
        var stock = await service.SearchProductStockByIdAsync(1, CancellationToken.None);
        Assert.NotNull(stock);
        Assert.Equal(new[] { "warehouse", "pvz" }, stock.Warehouses.Select(row => row.Warehouse.Type));
        Assert.Equal(new[] { 12, 3 }, stock.Warehouses.Select(row => row.Quantity));
        Assert.Empty((await service.SearchProductStockByIdAsync(2, CancellationToken.None))!.Warehouses);
        Assert.Empty((await service.SearchProductStockByIdAsync(3, CancellationToken.None))!.Warehouses);
        Assert.Null(await service.SearchProductStockByIdAsync(999999, CancellationToken.None));
    }

    [PointsPostgresFact]
    public async Task DatabaseRejectsDuplicateNegativeAndOrphanStockRows()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        foreach (var (sql, state) in new[]
        {
            ("INSERT INTO warehouse_products(logistics_points_id,product_id,quantity) VALUES(1,1,1)", "23505"),
            ("INSERT INTO warehouse_products(logistics_points_id,product_id,quantity) VALUES(4,1,-1)", "23514"),
            ("INSERT INTO warehouse_products(logistics_points_id,product_id,quantity) VALUES(999999,1,1)", "23503"),
            ("INSERT INTO warehouse_products(logistics_points_id,product_id,quantity) VALUES(1,999999,1)", "23503"),
            ("DELETE FROM logistics_points WHERE id=1", "23503")
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            // PostgreSQL versions distinguish RESTRICT from a missing referenced row.
            if (sql.StartsWith("DELETE", StringComparison.Ordinal))
                Assert.Contains(exception.SqlState, new[] { "23503", "23001" });
            else
                Assert.Equal(state, exception.SqlState);
        }
    }

    [PointsPostgresFact]
    public async Task SuppliedSqlRunsAndSmokeTestRollsBackItsRows()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        foreach (var file in new[] { "logistics-points-check.sql", "logistics-points-smoke.sql" })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", file));
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        await using var count = new NpgsqlCommand("SELECT count(*) FROM logistics_points", connection);
        Assert.Equal(8L, await count.ExecuteScalarAsync());
    }
}
