using System.Data;
using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.BoxMovements;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Logistics_Assistant.Services.BoxMovements;

public enum LoadBoxResult
{
    Loaded,
    InvalidRequest,
    OrderNotFound,
    NotResponsible,
    InvalidOrderStatus,
    InvalidSource,
    ItemNotFound,
    BoxNotFound,
    BoxUnavailable,
    Conflict
}

public sealed class BoxMovementService(ApplicationDbContext db)
{
    public async Task<LoadBoxResult> LoadAsync(
        long orderId,
        string userId,
        LoadBoxRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);

        var hasId = request.PhysicalBoxId.HasValue;
        var url = request.Url?.Trim();
        var hasUrl = !string.IsNullOrEmpty(url);

        if (orderId <= 0 || request.OrderItemId <= 0 ||
            request.PhysicalBoxId is <= 0 || hasId == hasUrl)
            return LoadBoxResult.InvalidRequest;

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var order = await db.DeliveryOrders.AsNoTracking()
                .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

            if (order is null) return LoadBoxResult.OrderNotFound;
            if (order.ResponsibleUserId != userId) return LoadBoxResult.NotResponsible;
            if (order.Status != "loading") return LoadBoxResult.InvalidOrderStatus;
            if (order.SourcePointId is not long sourcePointId) return LoadBoxResult.InvalidSource;

            var validSource = await db.LogisticsPoints.AnyAsync(point =>
                point.Id == sourcePointId && point.Type == "warehouse" && point.IsActive,
                cancellationToken);
            if (!validSource) return LoadBoxResult.InvalidSource;

            var item = await db.DeliveryOrderItems.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OrderItemId && item.OrderId == orderId,
                    cancellationToken);
            if (item is null) return LoadBoxResult.ItemNotFound;

            var boxes = db.PhysicalBoxes.AsQueryable();
            boxes = hasId
                ? boxes.Where(box => box.Id == request.PhysicalBoxId)
                : boxes.Where(box => box.Url == url);

            var box = await boxes.SingleOrDefaultAsync(cancellationToken);
            if (box is null) return LoadBoxResult.BoxNotFound;
            if (box.BoxId != item.BoxId) return LoadBoxResult.BoxUnavailable;

            if (box.Status == PhysicalBoxStatus.InTransit && box.CurrentOrderItemId == item.Id)
                return LoadBoxResult.Loaded;

            if (box.Status != PhysicalBoxStatus.AtPoint || box.CurrentPointId != sourcePointId ||
                (box.CurrentOrderItemId is not null && box.CurrentOrderItemId != item.Id))
                return LoadBoxResult.BoxUnavailable;

            box.Status = PhysicalBoxStatus.InTransit;
            box.CurrentPointId = null;
            box.CurrentOrderItemId = item.Id;
            db.BoxMovements.Add(new BoxMovement
            {
                PhysicalBoxId = box.Id,
                OrderItemId = item.Id,
                Operation = BoxMovementOperation.Load,
                PointId = sourcePointId,
                UserId = userId
            });

            await db.SaveChangesAsync(cancellationToken);

            var productId = await db.Boxes.Where(packaging => packaging.Id == box.BoxId)
                .Select(packaging => packaging.ProductId).SingleAsync(cancellationToken);
            var packaging = await db.Boxes.AsNoTracking()
                .Where(packaging => packaging.ProductId == productId)
                .Select(packaging => new
                {
                    packaging.QuantityPerProduct,
                    Count = db.PhysicalBoxes.Count(physicalBox => physicalBox.BoxId == packaging.Id &&
                        physicalBox.Status == PhysicalBoxStatus.AtPoint && physicalBox.CurrentPointId == sourcePointId)
                }).ToListAsync(cancellationToken);

            var quantity = packaging.Min(packaging => packaging.Count / packaging.QuantityPerProduct);
            var stock = await db.WarehouseProducts.SingleOrDefaultAsync(stock =>
                stock.LogisticsPointId == sourcePointId && stock.ProductId == productId, cancellationToken);

            if (stock is null)
            {
                db.WarehouseProducts.Add(new WarehouseProduct
                {
                    LogisticsPointId = sourcePointId,
                    ProductId = productId,
                    Quantity = quantity
                });
            }
            else
            {
                stock.Quantity = quantity;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoadBoxResult.Loaded;
        }
        catch (Exception exception) when (IsConcurrentChange(exception))
        {
            db.ChangeTracker.Clear();
            return LoadBoxResult.Conflict;
        }
    }

    private static bool IsConcurrentChange(Exception exception)
    {
        // Npgsql can wrap the PostgreSQL error in more than one exception.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState is "40001" or "40P01" or "23505" or "23503")
                return true;
        }

        return false;
    }
}
