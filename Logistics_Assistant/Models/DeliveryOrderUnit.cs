using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

[Table("delivery_order_units")]
public class DeliveryOrderUnit
{
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("order_need_id")]
    public long OrderNeedId { get; set; }

    [Column("product_unit_id")]
    public long ProductUnitId { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("released_at")]
    public DateTime? ReleasedAt { get; set; }
}
