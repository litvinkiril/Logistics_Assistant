using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("warehouse_products")]
    public class WarehouseProduct
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("logistics_points_id")]
        public long LogisticsPointId { get; set; }

        [Column("product_id")]
        public long ProductId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        public LogisticsPoint LogisticsPoint { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}