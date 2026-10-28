using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Logistics_Assistant.Tests;

public sealed class LogisticsPointsFixture : IAsyncLifetime
{
    private readonly string _databaseName = "points_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _created;
    public string ConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("LOGISTICS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnection)) return;
        // ICU gives predictable Cyrillic ILIKE behavior even if the cluster defaults to C.
        await Admin($"CREATE DATABASE {_databaseName} TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER icu ICU_LOCALE 'ru-RU'");
        _created = true;
        try
        {
            ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection)
            {
                Database = _databaseName,
                Pooling = false
            }.ConnectionString;
            Factory = new PointsApplicationFactory(ConnectionString);
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(ConnectionString, db.Database.GetConnectionString());
            await db.Database.MigrateAsync();
            db.LogisticsPoints.AddRange(
                Point(1, "Склад Северный", "warehouse", "Москва", "ул. Складская, 12"),
                Point(2, "ПВЗ Сокол", "pvz", "Москва", "ул. Алабяна, 8"),
                Point(3, "Склад Южный", "warehouse", "Тула", "ул. Южная, 3", false),
                Point(4, "ПВЗ Центр", "pvz", "Казань", "ул. Баумана, 5"),
                Point(5, "Склад 100%", "warehouse", "Омск", "ул. Процентная, 1"),
                Point(6, "ПВЗ_A", "pvz", "Омск", "ул. Лесная, 2"),
                Point(7, @"Склад \Архив", "warehouse", "Самара", "ул. Архивная, 2"),
                Point(8, "ПВЗ Сокол", "pvz", "Москва", "ул. Алабяна, 9", false));
            db.Products.AddRange(
                new Product { Id = 1, Name = "Тестовый шкаф" },
                new Product { Id = 2, Name = "Тестовый стол" },
                new Product { Id = 3, Name = "Товар без остатков" });
            await db.SaveChangesAsync();
            db.WarehouseProducts.AddRange(
                new WarehouseProduct { LogisticsPointId = 1, ProductId = 1, Quantity = 12 },
                new WarehouseProduct { LogisticsPointId = 2, ProductId = 1, Quantity = 3 },
                new WarehouseProduct { LogisticsPointId = 2, ProductId = 2, Quantity = 0 });
            await db.SaveChangesAsync();
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        if (_created)
        {
            await Admin($"DROP DATABASE {_databaseName}");
            _created = false;
        }
    }

    private async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static LogisticsPoint Point(long id, string name, string type, string city, string address, bool active = true) =>
        new() { Id = id, Name = name, Type = type, City = city, Address = address,
            Latitude = 55.75m, Longitude = 37.61m, IsActive = active };

    private sealed class PointsApplicationFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }
}

public sealed class PointsPostgresFactAttribute : FactAttribute
{
    public PointsPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOGISTICS_TEST_CONNECTION")))
            Skip = "Run scripts/test-logistics-points.ps1 to test against isolated PostgreSQL.";
    }
}

public sealed class PointsPostgresTheoryAttribute : TheoryAttribute
{
    public PointsPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOGISTICS_TEST_CONNECTION")))
            Skip = "Run scripts/test-logistics-points.ps1 to test against isolated PostgreSQL.";
    }
}
