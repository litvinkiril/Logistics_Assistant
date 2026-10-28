using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace Logistics_Assistant.Models
{
    [Table("physical_boxes")]
    [Index(nameof(Url), IsUnique = true, Name = "idx_physical_boxes_url")]
    public class PhysicalBox
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("box_id")]
        public long BoxId { get; set; }

        [Column("url", TypeName = "text")]
        public string Url { get; set; } = string.Empty;

        // Nullable while existing boxes are matched to real product units.
        [Column("product_id")]
        public long? ProductId { get; set; }

        [Column("product_unit_id")]
        public long? ProductUnitId { get; set; }

        [Column("current_point_id")]
        public long? CurrentPointId { get; set; }

        [Column("current_order_id")]
        public long? CurrentOrderId { get; set; }

        public Box Box { get; set; } = null!;
    }
}
