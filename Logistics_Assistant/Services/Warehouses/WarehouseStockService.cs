using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Products;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;

namespace Logistics_Assistant.Services.Warehouses
{
    public class WarehouseStockService
    {
        private readonly ApplicationDbContext _db;

        public WarehouseStockService(ApplicationDbContext db)
        {
            _db = db;
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
                        .Where(stock => stock.ProductId == product.Id)
                        .OrderBy(stock => stock.LogisticsPointId)
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

        public Task<List<LogisticsPoint>> GetWarehousesAsync(
            CancellationToken cancellationToken)
        {
            return _db.LogisticsPoints
                .AsNoTracking()
                .Where(point => point.Type == "warehouse")
                .OrderBy(point => point.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<LogisticsPoint?> GetWarehouseByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            return _db.LogisticsPoints
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    point => point.Id == id && point.Type == "warehouse",
                    cancellationToken);
        }

        public Task<List<LogisticsPoint>> SearchWarehousesByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var searchName = name.Trim();

            return _db.LogisticsPoints
                .AsNoTracking()
                .Where(point =>
                    point.Type == "warehouse" &&
                    EF.Functions.ILike(point.Name, $"%{searchName}%"))
                .OrderBy(point => point.Name)
                .ThenBy(point => point.Id)
                .ToListAsync(cancellationToken);
        }
    }
}