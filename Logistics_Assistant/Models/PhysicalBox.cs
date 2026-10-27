using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace Logistics_Assistant.Models
{
    [Table("physical_boxes")]
    [Index(nameof(Url), Name = "idx_physical_boxes_url")]
    public class PhysicalBox
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("box_id")]
        public long BoxId { get; set; }

        [Column("url", TypeName = "text")]
        public string Url { get; set; } = string.Empty;

        public Box Box { get; set; } = null!;
    }
}
