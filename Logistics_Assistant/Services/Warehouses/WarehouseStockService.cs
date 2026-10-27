using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Products;
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
    }
}