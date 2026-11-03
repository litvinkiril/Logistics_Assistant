using System.Data.Common;
using Logistics_Assistant.Data;
using Logistics_Assistant.Services.LogisticsPoints;
using Logistics_Assistant.Services.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "Cache")]
public sealed class CacheBehaviorTests(LogisticsPointsFixture fixture)
    : IClassFixture<LogisticsPointsFixture>, IDisposable
{
    private readonly ServiceProvider _services = TestCache.CreateServices();
    private HybridCache Cache => _services.GetRequiredService<HybridCache>();

    public void Dispose() => _services.Dispose();

    [PointsPostgresFact]
    public async Task ProductByIdQueriesDatabaseOnceAndSeparatesIds()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var search = new ProductSearchService(db, Cache);

        Assert.Equal("Тестовый шкаф", Assert.Single(await search.SearchByIdAsync(1, default)).Name);
        Assert.Equal(1, queries.Count);
        Assert.Equal(1, Assert.Single(await search.SearchByIdAsync(1, default)).Id);
        Assert.Equal(1, queries.Count); // A warm lookup must not execute SQL.
        Assert.Equal(2, Assert.Single(await search.SearchByIdAsync(2, default)).Id);
        Assert.Equal(2, queries.Count);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [PointsPostgresFact]
    public async Task ProductUrlUsesSameCacheEntryAfterTrimming()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var search = new ProductSearchService(db, Cache);
        const string url = "https://example.test/products/cabinet?ref=one&part=two";

        Assert.Equal(1, Assert.Single(await search.SearchByUrlAsync($"  {url}  ", default)).Id);
        Assert.Equal(1, Assert.Single(await search.SearchByUrlAsync(url, default)).Id);
        Assert.Equal(1, queries.Count);
        Assert.Empty(await search.SearchByUrlAsync("https://example.test/missing", default));
        Assert.Equal(2, queries.Count);
    }

    [PointsPostgresFact]
    public async Task MissingProductsAndPointsAreAlsoCached()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var products = new ProductSearchService(db, Cache);
        var points = new LogisticsPointService(db, Cache);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Empty(await products.SearchByIdAsync(999999, default));
            Assert.Empty(await products.SearchByUrlAsync("missing", default));
            Assert.Null(await points.GetPointByIdAsync(999999, default));
        }
        Assert.Equal(3, queries.Count);
    }

    [PointsPostgresFact]
    public async Task PointDetailsAreCachedSeparatelyById()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var points = new LogisticsPointService(db, Cache);

        Assert.Equal("warehouse", (await points.GetPointByIdAsync(1, default))!.Type);
        Assert.Equal("warehouse", (await points.GetPointByIdAsync(1, default))!.Type);
        Assert.Equal(1, queries.Count);
        Assert.Equal("pvz", (await points.GetPointByIdAsync(2, default))!.Type);
        Assert.Equal(2, queries.Count);
    }

    [PointsPostgresFact]
    public async Task PointSearchNormalizesWhitespaceAndSeparatesFilters()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var points = new LogisticsPointService(db, Cache);

        Assert.Equal(2, (await points.SearchPointsAsync(" Москва ", " pvz ", default)).Count);
        Assert.Equal(2, (await points.SearchPointsAsync("Москва", "pvz", default)).Count);
        Assert.Equal(1, queries.Count);
        Assert.Equal(1, Assert.Single(await points.SearchPointsAsync("Москва", "warehouse", default)).Id);
        Assert.Equal(4, Assert.Single(await points.SearchPointsAsync("Казань", "pvz", default)).Id);
        Assert.Equal(3, queries.Count);
    }

    [PointsPostgresFact]
    public async Task EmptyPointSearchAndListShareCacheEntry()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var points = new LogisticsPointService(db, Cache);

        Assert.Equal(8, (await points.GetPointsAsync(null, default)).Count);
        Assert.Equal(8, (await points.SearchPointsAsync("  ", "  ", default)).Count);
        Assert.Equal(1, queries.Count);
    }

    [PointsPostgresFact]
    public async Task PointTagInvalidatesDetailsAndSearchButKeepsProducts()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var points = new LogisticsPointService(db, Cache);
        var products = new ProductSearchService(db, Cache);

        await points.GetPointByIdAsync(1, default);
        await points.GetPointsAsync(null, default);
        await products.SearchByIdAsync(1, default);
        Assert.Equal(3, queries.Count);

        await Cache.RemoveByTagAsync("logistics-points");

        Assert.Equal(1, (await points.GetPointByIdAsync(1, default))!.Id);
        Assert.Equal(8, (await points.GetPointsAsync(null, default)).Count);
        Assert.Equal(5, queries.Count);
        Assert.Equal(1, Assert.Single(await products.SearchByIdAsync(1, default)).Id);
        Assert.Equal(5, queries.Count);
    }

    [PointsPostgresFact]
    public async Task RemovingProductKeyForcesDatabaseQuery()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var products = new ProductSearchService(db, Cache);

        await products.SearchByIdAsync(1, default);
        await Cache.RemoveAsync("products:by-id:v2:1");
        Assert.Equal(1, Assert.Single(await products.SearchByIdAsync(1, default)).Id);
        Assert.Equal(2, queries.Count);
    }

    [PointsPostgresFact]
    public async Task StockLookupAlwaysQueriesDatabase()
    {
        var queries = new QueryCounter();
        await using var db = CreateContext(queries);
        var points = new LogisticsPointService(db, Cache);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var stock = await points.SearchProductStockByIdAsync(1, default);
            Assert.Equal(new[] { 12, 3 }, stock!.Warehouses.Select(row => row.Quantity));
            Assert.Equal(attempt + 1, queries.Count);
        }
    }

    private ApplicationDbContext CreateContext(QueryCounter queries) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(queries)
            .Options);

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
