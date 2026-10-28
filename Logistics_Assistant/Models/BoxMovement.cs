using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

[Table("box_movements")]
public class BoxMovement
{
    [Column("id")]
    public long Id { get; set; }

    [Column("physical_box_id")]
    public long PhysicalBoxId { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("from_point_id")]
    public long? FromPointId { get; set; }

    [Column("to_point_id")]
    public long? ToPointId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
