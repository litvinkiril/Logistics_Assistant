using System.ComponentModel.DataAnnotations.Schema;

using System.ComponentModel.DataAnnotations;

namespace Logistics_Assistant.Models
{
    [Table("boxes")]
    public class Box
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("product_id")]
        public long ProductId { get; set; }

        [Column("name")]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [Column("quantity_per_product")]
        public int QuantityPerProduct { get; set; } = 1;

        [Column("width_cm", TypeName = "numeric(8,2)")]
        public decimal WidthCm { get; set; }

        [Column("height_cm", TypeName = "numeric(8,2)")]
        public decimal HeightCm { get; set; }

        [Column("length_cm", TypeName = "numeric(8,2)")]
        public decimal LengthCm { get; set; }

        [Column("weight_kg", TypeName = "numeric(8,3)")]
        public decimal WeightKg { get; set; }

        [Column("is_fragile")]
        public bool IsFragile { get; set; }

        public Product Product { get; set; } = null!;
    }
}
