using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Hybrid;
using Logistics_Assistant.DTOs.DeliveryOrders;

namespace Logistics_Assistant.Services.DeliveryOrders;

public enum AssignResponsibleResult
{
    Assigned,
    OrderNotFound,
    UserNotFound,
    AlreadyAssigned
}

public class DeliveryOrderService
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _db;
    private readonly HybridCache _cache;

    public DeliveryOrderService(
        ApplicationDbContext db, 
        HybridCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<List<DeliveryOrder>> GetUnassignedAsync(
        int offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        return _db.DeliveryOrders
            .AsNoTracking()
            .Where(order => order.ResponsibleUserId == null)
            .OrderBy(order => order.Id)
            .Skip(offset)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssignResponsibleResult> AssignResponsibleAsync(
        long orderId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var userExists = await _db.Users
            .AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
            return AssignResponsibleResult.UserNotFound;

        var updated = await _db.DeliveryOrders
            .Where(order =>
                order.Id == orderId &&
                order.ResponsibleUserId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    order => order.ResponsibleUserId,
                    userId),
                cancellationToken);

        if (updated > 0)
            return AssignResponsibleResult.Assigned;

        var orderExists = await _db.DeliveryOrders
            .AnyAsync(order => order.Id == orderId, cancellationToken);

        return orderExists
            ? AssignResponsibleResult.AlreadyAssigned
            : AssignResponsibleResult.OrderNotFound;
    }

    public async Task<DeliveryOrderDetailsResponse?> GetDetailsAsync(
    long orderId,
    CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);

        var order = await _db.DeliveryOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken);

        if (order is null)
            return null;

        var items = await (
            from item in _db.DeliveryOrderItems.AsNoTracking()
            join box in _db.Boxes
                on item.BoxId equals box.Id
            join product in _db.Products
                on box.ProductId equals product.Id
            join destination in _db.LogisticsPoints
                on item.DestinationPointId equals destination.Id
            where item.OrderId == orderId
            orderby destination.Id, product.Name, box.Id, item.Id
            select new DeliveryCargoDto
            {
                ItemId = item.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                BoxId = box.Id,
                BoxName = box.Name,
                BoxQuantity = item.Quantity,

                InTruckBoxQuantity = _db.PhysicalBoxes.Count(physicalBox =>
                    physicalBox.CurrentOrderItemId == item.Id &&
                    physicalBox.Status == PhysicalBoxStatus.InTransit),

                Destination = destination
            })
            .ToListAsync(cancellationToken);

        return new DeliveryOrderDetailsResponse
        {
            Order = order,
            Items = items
        };
    }
}