using Logistics_Assistant.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Logistics_Assistant.Configuration;

namespace Logistics_Assistant.Tests;

public sealed class LogisticsMigrationTests
{
    private const string Previous = "20261028113952_AddDeliveryOrderBoxes";
    private static readonly ServiceProvider ApplicationServices = CreateApplicationServices();

    [Fact]
    public void ModelMatchesLatestMigration()
    {
        using var db = CreateContext("Host=localhost;Database=unused;Username=unused");
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task UpgradePreservesLegacyRowsAndEnforcesTrackingConstraints()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.MigrateAsync();

        var legacyBox = await db.PhysicalBoxes.AsNoTracking().SingleAsync();
        Assert.Equal(1, legacyBox.ProductId);
        Assert.Null(legacyBox.ProductUnitId);
        Assert.Null(legacyBox.CurrentPointId);
        Assert.Null(legacyBox.CurrentOrderId);
        Assert.Equal(7, (await db.WarehouseProducts.SingleAsync()).Quantity);
        Assert.Equal(1, (await db.DeliveryOrders.SingleAsync()).Id);

        // Draft order does not require fictional vehicle data.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO delivery_orders (id,user_id,status,created_at)
            VALUES (2,'test-user','new',now());
            INSERT INTO need_to_deliver (id,product_id,quantity,logistic_point_id)
            VALUES (1,1,2,1);
            INSERT INTO delivery_order_needs (id,order_id,need_to_deliver_id,quantity,delivered_quantity)
            VALUES (1,1,1,1,0), (2,2,1,1,0);
            INSERT INTO product_units (id,product_id,expected_box_count,created_at)
            VALUES (1,1,2,now()), (2,2,1,now());
            UPDATE physical_boxes SET product_unit_id=1,current_point_id=1 WHERE id=1;
            INSERT INTO delivery_order_units (id,order_id,order_need_id,product_unit_id)
            VALUES (1,1,1,1);
            """);

        // One part is not a full product; a complete reserved unit is not available.
        Assert.Equal(0L, await Scalar(database.ConnectionString,
            "SELECT coalesce(sum(quantity),0)::bigint FROM logistics_unit_stock"));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO physical_boxes(id,box_id,url,product_id,product_unit_id,current_point_id)
            VALUES(3,2,'test/box/3',1,1,1);
            """);
        Assert.Equal(1L, await Scalar(database.ConnectionString, "SELECT quantity FROM logistics_unit_stock"));
        Assert.Equal(0L, await Scalar(database.ConnectionString, "SELECT available_quantity FROM logistics_unit_stock"));

        // Exact URL lookup, product compatibility and unambiguous physical location.
        await Reject(db, "INSERT INTO physical_boxes(id,box_id,url) VALUES(2,1,'test/box/1')", "23505");
        await Reject(db, "INSERT INTO products(id,name,url) VALUES(3,'Duplicate','test/product/1')", "23505");
        await Reject(db, "UPDATE physical_boxes SET product_unit_id=2 WHERE id=1", "23503");
        await Reject(db, "UPDATE physical_boxes SET current_order_id=1 WHERE id=1", "23514");
        await Reject(db, "INSERT INTO physical_boxes(id,box_id,url,product_id,product_unit_id) VALUES(2,1,'test/box/2',1,1)", "23505");
        await Reject(db, "INSERT INTO delivery_order_units(id,order_id,order_need_id,product_unit_id) VALUES(2,2,2,1)", "23505");
        await Reject(db, "INSERT INTO delivery_order_units(id,order_id,order_need_id,product_unit_id) VALUES(3,1,2,2)", "23503");
        await Reject(db, "INSERT INTO delivery_order_boxes(order_id,physical_box_id,point_id,status) VALUES(2,1,1,1)", "23505");
        await Reject(db, "UPDATE delivery_order_boxes SET order_need_id=2 WHERE order_id=1", "23503");
        await Reject(db, "UPDATE delivery_order_boxes SET unloaded_at=now() WHERE order_id=1", "23514");
        await Reject(db, "INSERT INTO need_to_deliver(id,product_id,quantity,logistic_point_id) VALUES(999,999,1,1)", "23503");
        await Reject(db, "UPDATE delivery_order_needs SET delivered_quantity=2 WHERE id=1", "23514");

        // Releasing a reservation permits a later assignment without losing history.
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE delivery_order_units SET released_at=now() WHERE id=1;
            INSERT INTO delivery_order_units(id,order_id,order_need_id,product_unit_id) VALUES(2,2,2,1);
            UPDATE delivery_order_boxes SET cancelled_at=now() WHERE order_id=1;
            INSERT INTO delivery_order_boxes(order_id,physical_box_id,point_id,status) VALUES(2,1,1,1);
            """);
        Assert.Equal(2, await db.DeliveryOrderUnits.CountAsync());
        Assert.Equal(1, await db.NeedToDeliverItems.CountAsync());

        // The operation writes location, scan timestamp and movement together.
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE physical_boxes SET current_point_id=NULL,current_order_id=2 WHERE id=1;
                UPDATE delivery_order_boxes SET loaded_at=now() WHERE order_id=2;
                INSERT INTO box_movements(physical_box_id,order_id,from_point_id,user_id,occurred_at)
                VALUES(1,2,1,'test-user',now());
                """);
            await transaction.CommitAsync();
        }
        Assert.Equal(1, await db.BoxMovements.CountAsync());
        Assert.Equal(0L, await Scalar(database.ConnectionString,
            "SELECT coalesce(sum(quantity),0)::bigint FROM logistics_unit_stock"));
        await Reject(db, "INSERT INTO box_movements(physical_box_id,order_id,from_point_id,to_point_id,user_id,occurred_at) VALUES(1,2,1,1,'test-user',now())", "23514");

        var rollbackError = await Assert.ThrowsAsync<PostgresException>(
            () => db.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Contains("Rollback would lose logistics data", rollbackError.MessageText);
        Assert.Equal(1, await db.BoxMovements.CountAsync());
    }

    [PostgresFact]
    public async Task DuplicateLegacyLabelsAbortUpgradeWithoutLosingData()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO physical_boxes(id,box_id,url) VALUES(2,1,'test/box/1')");

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Contains("Duplicate physical_boxes.url", error.MessageText);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM physical_boxes", connection);
        Assert.Equal(2L, await count.ExecuteScalarAsync());
        Assert.Equal(Previous, (await db.Database.GetAppliedMigrationsAsync()).Last());
    }

    [PostgresFact]
    public async Task UnusedUpgradeCanBeRolledBackAndReapplied()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = CreateContext(database.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        await db.Database.ExecuteSqlRawAsync(LegacySeed);
        await db.Database.MigrateAsync();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        await db.Database.MigrateAsync();
        Assert.Equal(1, await db.PhysicalBoxes.CountAsync());
    }

    private static async Task Reject(ApplicationDbContext db, string sql, string sqlState)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(sqlState, error.SqlState);
    }

    private static async Task<long> Scalar(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static ApplicationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseApplicationServiceProvider(ApplicationServices)
            .UseNpgsql(connectionString).Options);

    private static ServiceProvider CreateApplicationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        IdentityConfiguration.ConfigureIdentity(services);
        return services.BuildServiceProvider();
    }

    private const string LegacySeed = """
        INSERT INTO "AspNetUsers" ("Id","AccessFailedCount","EmailConfirmed","LockoutEnabled","PhoneNumberConfirmed","TwoFactorEnabled")
        VALUES ('test-user',0,false,false,false,false);
        INSERT INTO products(id,name,url) VALUES(1,'Test cabinet','test/product/1'),(2,'Other product',NULL);
        INSERT INTO logistics_points(id,name,type,address,city,latitude,longitude,is_active)
        VALUES(1,'Test point','warehouse','Test address','Test city',0,0,true);
        INSERT INTO boxes(id,product_id,box_number,width_cm,height_cm,length_cm,weight_kg,is_fragile)
        VALUES(1,1,1,10,10,10,1,false),(2,1,2,10,10,10,1,false),(3,2,1,10,10,10,1,false);
        INSERT INTO physical_boxes(id,box_id,url) VALUES(1,1,'test/box/1');
        INSERT INTO warehouse_products(id,logistics_points_id,product_id,quantity) VALUES(1,1,1,7);
        INSERT INTO delivery_orders(id,user_id,car_plate,contact_phone,cargo_length_cm,cargo_width_cm,cargo_height_cm,max_load_kg,status,created_at)
        VALUES(1,'test-user','TEST','123',300,200,200,1000,'new',now());
        INSERT INTO delivery_order_boxes(order_id,physical_box_id,point_id,status) VALUES(1,1,1,1);
        """;

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnection;
        private readonly string _name = "logistics_test_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; }

        private TestDatabase(string adminConnection)
        {
            _adminConnection = adminConnection;
            var builder = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = _name,
                Pooling = false
            };
            ConnectionString = builder.ConnectionString;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase(Environment.GetEnvironmentVariable("LOGISTICS_TEST_CONNECTION")!);
            await database.ExecuteAdmin($"CREATE DATABASE {database._name}");
            return database;
        }

        public async ValueTask DisposeAsync() => await ExecuteAdmin($"DROP DATABASE {_name}");

        private async Task ExecuteAdmin(string sql)
        {
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOGISTICS_TEST_CONNECTION")))
            Skip = "Set LOGISTICS_TEST_CONNECTION to a disposable PostgreSQL server (CREATE DATABASE permission).";
    }
}
