using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Logistics_Assistant.Tests;

public sealed partial class LogisticsMigrationTests
{
    private const string Mvp = "20261104101802_SimplifyLogisticsMvp";

    [Fact]
    public void MvpModelContainsEightLogisticsTables()
    {
        using var db = CreateContext("Host=localhost;Database=unused;Username=unused");
        var tables = db.Model.GetEntityTypes()
            .Where(entity => entity.ClrType.Namespace == typeof(Product).Namespace)
            .Select(entity => entity.GetTableName()).Order().ToArray();
        Assert.Equal(new[]
        {
            "box_movements", "boxes", "delivery_order_items", "delivery_orders",
            "logistics_points", "physical_boxes", "products", "warehouse_products"
        }, tables);
    }

    [PostgresFact]
    public async Task MvpUpgradePreservesStockLabelsLocationsAndAssignments()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Responsible);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE physical_boxes SET current_point_id=1 WHERE id=1;
            INSERT INTO physical_boxes(id,box_id,url,current_point_id)
            VALUES (2,1,'test/box/2',1);
            INSERT INTO delivery_order_boxes(order_id,physical_box_id,point_id,status)
            VALUES (1,2,1,1);
            """);
        await db.Database.MigrateAsync();

        var packaging = await db.Boxes.AsNoTracking().SingleAsync(box => box.Id == 1);
        Assert.Equal("Коробка 1", packaging.Name);
        Assert.Equal(1, packaging.QuantityPerProduct);
        Assert.Equal(10m, packaging.WidthCm);
        Assert.Equal(1m, packaging.WeightKg);
        Assert.Equal("test/product/1", (await db.Products.SingleAsync(product => product.Id == 1)).Url);
        Assert.Equal(7, (await db.WarehouseProducts.SingleAsync()).Quantity);
        Assert.Equal("test-user", (await db.DeliveryOrders.SingleAsync()).UserId);

        var item = await db.DeliveryOrderItems.SingleAsync();
        Assert.Equal(1, item.OrderId);
        Assert.Equal(1, item.BoxId);
        Assert.Equal(1, item.DestinationPointId);
        Assert.Equal(2, item.Quantity);
        var physical = await db.PhysicalBoxes.OrderBy(box => box.Id).ToListAsync();
        Assert.Equal(new[] { "test/box/1", "test/box/2" }, physical.Select(box => box.Url));
        Assert.All(physical, box =>
        {
            Assert.Equal(1, box.CurrentPointId);
            Assert.Equal(item.Id, box.CurrentOrderItemId);
            Assert.Equal(PhysicalBoxStatus.AtPoint, box.Status);
        });
        Assert.Equal(8L, await Scalar(database.ConnectionString, """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema='public' AND table_type='BASE TABLE'
              AND table_name <> '__EFMigrationsHistory' AND table_name NOT LIKE 'AspNet%'
            """));
        Assert.Equal(0L, await Scalar(database.ConnectionString, """
            SELECT count(*) FROM information_schema.views
            WHERE table_schema='public' AND table_name='logistics_unit_stock'
            """));
    }

    [PostgresFact]
    public async Task MvpHistorySurvivesReassignmentToAnotherTrip()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Responsible);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE delivery_order_boxes SET status=2,loaded_at='2026-11-01T10:00:00Z';
            UPDATE physical_boxes SET current_order_id=1 WHERE id=1;
            INSERT INTO box_movements(id,physical_box_id,order_id,from_point_id,user_id,occurred_at)
            VALUES(20,1,1,1,'test-user','2026-11-01T10:00:00Z');
            """);
        await db.Database.MigrateAsync();

        var physical = await db.PhysicalBoxes.SingleAsync();
        Assert.Equal(PhysicalBoxStatus.InTransit, physical.Status);
        Assert.Null(physical.CurrentPointId);
        var oldItemId = physical.CurrentOrderItemId!.Value;
        var oldMovement = await db.BoxMovements.SingleAsync();
        Assert.Equal(20, oldMovement.Id);
        Assert.Equal(oldItemId, oldMovement.OrderItemId);
        Assert.Equal(BoxMovementOperation.Load, oldMovement.Operation);
        Assert.Equal(1, oldMovement.PointId);
        Assert.Equal("test-user", oldMovement.UserId);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc), oldMovement.OccurredAt);

        // Direct DB writes exercise storage only; no scanning workflow is implemented here.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO delivery_orders(id,user_id,status,created_at)
            VALUES(2,'test-user','new',now());
            """);
        var nextItem = new DeliveryOrderItem
        {
            OrderId = 2, BoxId = 1, DestinationPointId = 1, Quantity = 1
        };
        db.DeliveryOrderItems.Add(nextItem);
        await db.SaveChangesAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            physical.CurrentPointId = 1;
            physical.CurrentOrderItemId = null;
            physical.Status = PhysicalBoxStatus.AtPoint;
            db.BoxMovements.Add(new BoxMovement
            {
                PhysicalBoxId = physical.Id, OrderItemId = oldItemId,
                Operation = BoxMovementOperation.Unload, PointId = 1,
                UserId = "test-user", OccurredAt = new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        physical.CurrentOrderItemId = nextItem.Id;
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.BoxMovements.CountAsync(movement => movement.OrderItemId == oldItemId));
        Assert.Equal(1, (await db.DeliveryOrderItems.SingleAsync(item => item.Id == oldItemId)).OrderId);
        Assert.Equal(nextItem.Id, (await db.PhysicalBoxes.AsNoTracking().SingleAsync()).CurrentOrderItemId);
        Assert.Equal(7, (await db.WarehouseProducts.SingleAsync()).Quantity);
        var deleteError = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM delivery_order_items WHERE id={oldItemId}"));
        Assert.Contains(deleteError.SqlState, new[] { "23503", "23001" });
    }

    [PostgresFact]
    public async Task MvpUpgradeConvertsBothMovementDirectionsWithoutChangingDates()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Responsible);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE delivery_order_boxes SET status=4,loaded_at='2026-11-01T10:00:00Z',
                unloaded_at='2026-11-01T11:00:00Z';
            UPDATE physical_boxes SET current_point_id=1;
            INSERT INTO box_movements(id,physical_box_id,order_id,from_point_id,to_point_id,user_id,occurred_at)
            VALUES (30,1,1,1,NULL,'test-user','2026-11-01T10:00:00Z'),
                   (31,1,1,NULL,1,'test-user','2026-11-01T11:00:00Z');
            """);
        await db.Database.MigrateAsync();
        var movements = await db.BoxMovements.OrderBy(movement => movement.OccurredAt).ToListAsync();
        Assert.Equal(new[] { BoxMovementOperation.Load, BoxMovementOperation.Unload },
            movements.Select(movement => movement.Operation));
        Assert.Equal(new long[] { 30, 31 }, movements.Select(movement => movement.Id));
        Assert.Equal(new[] { 10, 11 }, movements.Select(movement => movement.OccurredAt.Hour));
        Assert.All(movements, movement => Assert.Equal(1, movement.PointId));
        var physical = await db.PhysicalBoxes.SingleAsync();
        Assert.Null(physical.CurrentOrderItemId);
        Assert.Equal(PhysicalBoxStatus.AtPoint, physical.Status);
    }

    [PostgresFact]
    public async Task MvpSchemaRejectsInvalidPackagingAssignmentsAndMovements()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Responsible);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.MigrateAsync();
        await Reject(db, "UPDATE boxes SET quantity_per_product=0 WHERE id=1", "23514");
        await Reject(db, "UPDATE boxes SET name=' ' WHERE id=1", "23514");
        await Reject(db, "UPDATE boxes SET name='Коробка 1' WHERE id=2", "23505");
        await Reject(db, "UPDATE delivery_order_items SET quantity=0", "23514");
        await Reject(db, "UPDATE delivery_order_items SET destination_point_id=999", "23503");
        await Reject(db, "UPDATE physical_boxes SET status=2,current_order_item_id=NULL", "23514");
        await Reject(db, "UPDATE physical_boxes SET status=9", "23514");
        await Reject(db, "INSERT INTO physical_boxes(id,box_id,url) VALUES(99,1,'test/box/1')", "23505");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO delivery_order_items(id,order_id,box_id,destination_point_id,quantity)
            VALUES(100,1,2,1,1);
            UPDATE boxes SET quantity_per_product=2 WHERE id=1;
            UPDATE physical_boxes SET status=1,current_point_id=1 WHERE id=1;
            INSERT INTO physical_boxes(id,box_id,url,current_point_id,status)
            VALUES(10,1,'test/box/10',1,1);
            """);
        await Reject(db, "UPDATE physical_boxes SET current_order_item_id=100 WHERE id=1", "23503");
        await Reject(db, "UPDATE physical_boxes SET status=2 WHERE id=1", "23514");
        await Reject(db, """
            INSERT INTO box_movements(physical_box_id,order_item_id,operation,point_id,user_id,occurred_at)
            VALUES(1,100,3,1,'test-user',now())
            """, "23514");
        await Reject(db, """
            INSERT INTO box_movements(physical_box_id,order_item_id,operation,point_id,user_id,occurred_at)
            VALUES(999,100,1,1,'test-user',now())
            """, "23503");
        Assert.Equal(2, (await db.Boxes.SingleAsync(box => box.Id == 1)).QuantityPerProduct);
        Assert.Equal(2, await db.PhysicalBoxes.CountAsync(box => box.BoxId == 1));
    }

    [PostgresFact]
    public async Task MvpUpgradeAbortsAtomicallyForUnconvertibleLegacyData()
    {
        string[] changes =
        [
            "INSERT INTO need_to_deliver(id,box_id,quantity,logistic_point_id) VALUES(1,1,1,1)",
            "INSERT INTO product_units(id,product_id,expected_box_count,created_at) VALUES(1,1,2,now())",
            "UPDATE delivery_order_boxes SET cancelled_at=now()",
            "UPDATE delivery_order_boxes SET status=2,loaded_at=now()",
            "DELETE FROM delivery_order_boxes; UPDATE physical_boxes SET current_order_id=1"
        ];
        foreach (var change in changes)
        {
            await using var database = await TestDatabase.CreateAsync();
            await using var db = CreateContext(database.ConnectionString);
            await db.GetService<IMigrator>().MigrateAsync(Responsible);
            await db.Database.ExecuteSqlRawAsync(LegacySeed);
            await db.Database.ExecuteSqlRawAsync(change);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("Cannot simplify logistics", error.MessageText);
            Assert.Equal(Responsible, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(7L, await Scalar(database.ConnectionString, "SELECT quantity::bigint FROM warehouse_products"));
            Assert.Equal(1L, await Scalar(database.ConnectionString, "SELECT box_number::bigint FROM boxes WHERE id=1"));
            Assert.Equal(1L, await Scalar(database.ConnectionString, "SELECT count(*) FROM physical_boxes"));
            Assert.Equal(0L, await Scalar(database.ConnectionString, """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema='public' AND table_name='delivery_order_items'
                """));
        }
    }

    [PostgresFact]
    public async Task MvpDowngradeIsBlockedBeforeAnyDataIsChanged()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Responsible);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.MigrateAsync();
        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => db.GetService<IMigrator>().MigrateAsync(Responsible));
        Assert.Contains("forward-only", error.Message);
        Assert.Equal(Mvp, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(7, (await db.WarehouseProducts.SingleAsync()).Quantity);
        Assert.Single(await db.DeliveryOrderItems.ToListAsync());
    }
}
