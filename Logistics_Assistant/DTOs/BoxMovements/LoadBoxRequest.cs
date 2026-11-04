namespace Logistics_Assistant.DTOs.BoxMovements;

public sealed class LoadBoxRequest
{
    public long OrderItemId { get; set; }
    public long? PhysicalBoxId { get; set; }
    public string? Url { get; set; }
}
