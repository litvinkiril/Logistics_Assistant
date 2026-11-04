using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models
{
    public enum PhysicalBoxStatus
    {
        Unknown = 0,
        AtPoint = 1,
        InTransit = 2
    }

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

        [Column("current_point_id")]
        public long? CurrentPointId { get; set; }

        [Column("current_order_item_id")]
        public long? CurrentOrderItemId { get; set; }

        [Column("status")]
        public PhysicalBoxStatus Status { get; set; } = PhysicalBoxStatus.Unknown;

        public Box Box { get; set; } = null!;
    }
}
