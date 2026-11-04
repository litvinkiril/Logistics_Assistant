using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

[Table("delivery_order_items")]
public class DeliveryOrderItem
{
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("box_id")]
    public long BoxId { get; set; }

    [Column("destination_point_id")]
    public long DestinationPointId { get; set; }

    // Planned physical boxes, not complete products.
    [Column("quantity")]
    public int Quantity { get; set; }
}
