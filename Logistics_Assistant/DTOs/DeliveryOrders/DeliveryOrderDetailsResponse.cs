using Logistics_Assistant.Models;

namespace Logistics_Assistant.DTOs.DeliveryOrders;

public sealed class DeliveryOrderDetailsResponse
{
    public DeliveryOrder Order { get; set; } = null!;
    public List<DeliveryCargoDto> Items { get; set; } = [];
}

public sealed class DeliveryCargoDto
{
    public long ItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long BoxId { get; set; }
    public string BoxName { get; set; } = string.Empty;

    public int BoxQuantity { get; set; }
    public int InTruckBoxQuantity { get; set; }

    public LogisticsPoint Destination { get; set; } = null!;
}