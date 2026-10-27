using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("boxes")]
    public class Box
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("product_id")]
        public long ProductId { get; set; }

        [Column("box_number")]
        public int BoxNumber { get; set; }

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