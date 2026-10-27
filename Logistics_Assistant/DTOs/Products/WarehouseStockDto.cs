using Logistics_Assistant.Models;

namespace Logistics_Assistant.DTOs.Products
{
    public class WarehouseStockDto
    {
        public required LogisticsPoint Warehouse { get; init; }

        public int Quantity { get; init; }
    }
}