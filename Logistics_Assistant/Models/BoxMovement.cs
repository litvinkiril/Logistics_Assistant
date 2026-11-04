using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

public enum BoxMovementOperation
{
    Load = 1,
    Unload = 2
}

[Table("box_movements")]
public class BoxMovement
{
    [Column("id")]
    public long Id { get; set; }

    [Column("physical_box_id")]
    public long PhysicalBoxId { get; set; }

    [Column("order_item_id")]
    public long OrderItemId { get; set; }

    [Column("operation")]
    public BoxMovementOperation Operation { get; set; }

    // Source for loading, destination for unloading; the other endpoint is the vehicle.
    [Column("point_id")]
    public long PointId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
