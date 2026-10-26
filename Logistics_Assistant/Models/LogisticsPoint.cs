using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    [Table("logistics_points")]
    public class LogisticsPoint
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [Column("address", TypeName = "text")]
        public string Address { get; set; } = string.Empty;

        [Column("city")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Column("latitude", TypeName = "numeric(9,6)")]
        public decimal Latitude { get; set; }

        [Column("longitude", TypeName = "numeric(9,6)")]
        public decimal Longitude { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}