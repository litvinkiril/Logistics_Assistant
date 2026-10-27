using Logistics_Assistant.Models;

namespace Logistics_Assistant.DTOs.Products
{
    public class ProductStockResponse
    {
        public required Product Product { get; init; }

        public List<WarehouseStockDto> Warehouses { get; init; }
            = new();
    }
}