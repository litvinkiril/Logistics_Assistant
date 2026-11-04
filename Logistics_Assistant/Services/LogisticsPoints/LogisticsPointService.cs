using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Logistics_Assistant.DTOs.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Logistics_Assistant.Services.LogisticsPoints
{
    public class LogisticsPointService
    {
        private readonly ApplicationDbContext _db;
        private readonly HybridCache _cache;

        private static readonly HybridCacheEntryOptions PointCacheOptions = new()
        {
            Expiration = TimeSpan.FromMinutes(10),
            LocalCacheExpiration = TimeSpan.FromMinutes(1)
        };

        public LogisticsPointService(
            ApplicationDbContext db,
            HybridCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public Task<ProductStockResponse?> SearchProductStockByIdAsync(
            long productId,
            CancellationToken cancellationToken)
        {
            return _db.Products
                .AsNoTracking()
                .Where(product => product.Id == productId)
                .Select(product => new ProductStockResponse
                {
                    Product = product,
                    Warehouses = _db.WarehouseProducts
                        .Where(stock => stock.ProductId == product.Id && stock.Quantity > 0)
                        .OrderByDescending(stock => stock.Quantity)
                        .ThenBy(stock => stock.LogisticsPointId)
                        .ThenBy(stock => stock.Id)
                        .Select(stock => new WarehouseStockDto
                        {
                            Warehouse = stock.LogisticsPoint,
                            Quantity = stock.Quantity
                        })
                        .ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task<List<LogisticsPoint>> GetPointsAsync(
            string? type,
            CancellationToken cancellationToken)
        {
            return SearchPointsAsync(
                query: null,
                type: type,
                cancellationToken: cancellationToken);
        }

        public async Task<LogisticsPoint?> GetPointByIdAsync(
        long id,
        CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            var key = $"logistics-points:by-id:v1:{id}";

            return await _cache.GetOrCreateAsync<LogisticsPoint?>(
                key,
                async token => await _db.LogisticsPoints
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        point => point.Id == id,
                        token),
                options: PointCacheOptions,
                tags: ["logistics-points"],
                cancellationToken: cancellationToken);
        }

        public async Task<List<LogisticsPoint>> SearchPointsAsync(
            string? query,
            string? type,
            CancellationToken cancellationToken)
        {
            var searchQuery = query?.Trim() ?? string.Empty;
            var pointType = type?.Trim() ?? string.Empty;

            var key =
                $"logistics-points:search:v1:" +
                $"{pointType.Length}:{pointType}:" +
                $"{searchQuery.Length}:{searchQuery}";

            return await _cache.GetOrCreateAsync<List<LogisticsPoint>>(
                key,
                async token =>
                {
                    var points = _db.LogisticsPoints.AsNoTracking();

                    if (pointType.Length > 0)
                    {
                        points = points.Where(
                            point => point.Type == pointType);
                    }

                    if (searchQuery.Length > 0)
                    {
                        var text = searchQuery
                            .Replace("\\", "\\\\")
                            .Replace("%", "\\%")
                            .Replace("_", "\\_");

                        var pattern = $"%{text}%";

                        points = points.Where(point =>
                            EF.Functions.ILike(point.Name, pattern, "\\") ||
                            EF.Functions.ILike(point.City, pattern, "\\") ||
                            EF.Functions.ILike(point.Address, pattern, "\\"));
                    }

                    return await points
                        .OrderBy(point => point.Name)
                        .ThenBy(point => point.Id)
                        .ToListAsync(token);
                },
                options: PointCacheOptions,
                tags: ["logistics-points"],
                cancellationToken: cancellationToken);
        }
    }
}
