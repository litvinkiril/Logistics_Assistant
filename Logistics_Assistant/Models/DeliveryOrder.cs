using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("delivery_orders")]
    public class DeliveryOrder
    {
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("car_plate")]
        [MaxLength(20)]
        public string? CarPlate { get; set; }

        [Column("contact_phone")]
        [MaxLength(32)]
        public string? ContactPhone { get; set; }

        [Column("cargo_length_cm", TypeName = "numeric(8,2)")]
        public decimal? CargoLengthCm { get; set; }

        [Column("cargo_width_cm", TypeName = "numeric(8,2)")]
        public decimal? CargoWidthCm { get; set; }

        [Column("cargo_height_cm", TypeName = "numeric(8,2)")]
        public decimal? CargoHeightCm { get; set; }

        [Column("max_load_kg", TypeName = "numeric(10,2)")]
        public decimal? MaxLoadKg { get; set; }

        [Column("source_point_id")]
        public long? SourcePointId { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "new";

        [Column("created_at", TypeName = "timestamp with time zone")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
