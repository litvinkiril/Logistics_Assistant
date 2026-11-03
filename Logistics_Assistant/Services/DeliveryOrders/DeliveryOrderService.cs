using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Hybrid;

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
}