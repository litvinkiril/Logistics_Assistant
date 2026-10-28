using System.ComponentModel.DataAnnotations.Schema;

namespace Logistics_Assistant.Models;

[Table("product_units")]
public class ProductUnit
{
    [Column("id")]
    public long Id { get; set; }

    [Column("product_id")]
    public long ProductId { get; set; }

    // Fixed at registration. Existing packaging definitions must not be repurposed.
    [Column("expected_box_count")]
    public int ExpectedBoxCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
