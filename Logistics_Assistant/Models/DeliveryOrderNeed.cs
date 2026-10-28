using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

[Table("delivery_order_needs")]
public class DeliveryOrderNeed
{
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("need_to_deliver_id")]
    public long NeedToDeliverId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    // Units of the original need: full products OR individual packaging parts.
    [Column("delivered_quantity")]
    public int DeliveredQuantity { get; set; }

    // Releases the unfulfilled remainder; keeps actual delivery history.
    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }
}
