using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Auth;
using Logistics_Assistant.DTOs.BoxMovements;
using Logistics_Assistant.Models;
using Logistics_Assistant.Services.BoxMovements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "BoxLoading")]
public sealed class BoxLoadingTests(LogisticsPointsFixture fixture) : IClassFixture<LogisticsPointsFixture>
{
    private static long _productId = 1000;

    [PointsPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadByIdOrUrlMovesBoxRecordsActorAndUpdatesStock(bool byUrl)
    {
        var data = await Seed();
        using var response = await Load(data, byUrl: byUrl);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var box = await db.PhysicalBoxes.FindAsync(data.BoxIds[0]);
        Assert.Equal(PhysicalBoxStatus.InTransit, box!.Status);
        Assert.Null(box.CurrentPointId);
        Assert.Equal(data.ItemIds[0], box.CurrentOrderItemId);
        var movement = await db.BoxMovements.SingleAsync(x => x.PhysicalBoxId == box.Id);
        Assert.Equal(BoxMovementOperation.Load, movement.Operation);
        Assert.Equal(data.ItemIds[0], movement.OrderItemId);
        Assert.Equal(1, movement.PointId);
        Assert.Equal(data.UserId, movement.UserId);
        Assert.Equal(0, (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
        Assert.Equal(1, (await db.DeliveryOrderItems.FindAsync(data.ItemIds[0]))!.Quantity);
    }

    [PointsPostgresFact]
    public async Task RepeatingScanByDifferentSelectorDoesNotDuplicateMovement()
    {
        var data = await Seed();
        using var first = await Load(data);
        using var second = await Load(data, byUrl: true);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.BoxMovements.CountAsync(x => x.PhysicalBoxId == data.BoxIds[0]));
        Assert.Equal(0, (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
    }

    [PointsPostgresTheory]
    [InlineData(null, null)]
    [InlineData(null, "  ")]
    [InlineData(0L, null)]
    [InlineData(-1L, null)]
    [InlineData(42L, "test/box/42")]
    public async Task InvalidSelectorsReturn400(long? id, string? url)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/delivery-orders/1/boxes/load",
            new LoadBoxRequest { OrderItemId = 1, PhysicalBoxId = id, Url = url });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PointsPostgresFact]
    public async Task AuthenticationAndCsrfAreRequired()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        var request = new LoadBoxRequest { OrderItemId = 1, PhysicalBoxId = 1 };
        using var anonymous = await client.PostAsJsonAsync("/api/delivery-orders/1/boxes/load", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        await AuthTestClient.Register(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var noToken = await client.PostAsJsonAsync("/api/delivery-orders/1/boxes/load", request);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
    }

    [PointsPostgresFact]
    public async Task AnotherUserCannotLoadTheOrder()
    {
        var data = await Seed();
        using var client = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(client);
        using var response = await client.PostAsJsonAsync(Path(data), Request(data));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNotMoved(data);
    }

    [PointsPostgresTheory]
    [InlineData("new")]
    [InlineData("unloading")]
    [InlineData("completed")]
    public async Task LoadingRequiresLoadingOrderStatus(string status)
    {
        var data = await Seed(status: status);
        using var response = await Load(data);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNotMoved(data);
    }

    [PointsPostgresTheory]
    [InlineData(null)]
    [InlineData(2L)]
    [InlineData(3L)]
    public async Task SourceMustBeAnActiveWarehouse(long? source)
    {
        var data = await Seed();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.DeliveryOrders.FindAsync(data.OrderId))!.SourcePointId = source;
            await db.SaveChangesAsync();
        }
        using var response = await Load(data);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNotMoved(data);
    }

    [PointsPostgresFact]
    public async Task CannotLoadFromAnotherWarehouseOrUseAnotherOrdersItem()
    {
        var data = await Seed();
        var other = await Seed();
        var request = Request(data);
        request.OrderItemId = other.ItemIds[0];
        using var wrongItem = await fixture.Client.PostAsJsonAsync(Path(data), request);
        Assert.Equal(HttpStatusCode.NotFound, wrongItem.StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.PhysicalBoxes.FindAsync(data.BoxIds[0]))!.CurrentPointId = 5;
            await db.SaveChangesAsync();
        }
        using var wrongWarehouse = await Load(data);
        Assert.Equal(HttpStatusCode.Conflict, wrongWarehouse.StatusCode);
        using var verification = fixture.Factory.Services.CreateScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(5, (await verifyDb.PhysicalBoxes.FindAsync(data.BoxIds[0]))!.CurrentPointId);
        Assert.False(await verifyDb.BoxMovements.AnyAsync(x => x.PhysicalBoxId == data.BoxIds[0]));
    }

    [PointsPostgresFact]
    public async Task AnotherPackagingTypeAndReservationAreRejected()
    {
        var data = await Seed(twoTypes: true);
        using var wrongPackaging = await Load(data, boxIndex: 1);
        Assert.Equal(HttpStatusCode.Conflict, wrongPackaging.StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original = await db.DeliveryOrderItems.FindAsync(data.ItemIds[0]);
            var reserved = new DeliveryOrderItem
            {
                OrderId = data.OrderId, BoxId = original!.BoxId, DestinationPointId = 4, Quantity = 1
            };
            db.DeliveryOrderItems.Add(reserved);
            await db.SaveChangesAsync();
            (await db.PhysicalBoxes.FindAsync(data.BoxIds[0]))!.CurrentOrderItemId = reserved.Id;
            await db.SaveChangesAsync();
        }
        using var reservedResponse = await Load(data);
        Assert.Equal(HttpStatusCode.Conflict, reservedResponse.StatusCode);
        using var verification = fixture.Factory.Services.CreateScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verifyDb.BoxMovements.AnyAsync(x => data.BoxIds.Contains(x.PhysicalBoxId)));
    }

    [PointsPostgresFact]
    public async Task StockUsesCompleteProductsAndIntegerDivisionAcrossAllPackagingTypes()
    {
        var data = await Seed(completeProducts: 2, firstTypePerProduct: 2, twoTypes: true);
        foreach (var (boxIndex, itemIndex) in new[] { (0, 0), (1, 0), (4, 1) })
        {
            var request = Request(data, boxIndex);
            request.OrderItemId = data.ItemIds[itemIndex];
            using var response = await fixture.Client.PostAsJsonAsync(Path(data), request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
        }
    }

    [PointsPostgresFact]
    public async Task MissingAggregateStockRowIsCreated()
    {
        var data = await Seed();
        using (var scope = fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WarehouseProducts
                .Where(x => x.ProductId == data.ProductId).ExecuteDeleteAsync();
        using var response = await Load(data);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var verification = fixture.Factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
    }

    [PointsPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentLoadsDoNotDuplicateMovementsOrLoseStockUpdates(bool differentBoxes)
    {
        var data = await Seed(completeProducts: 2);
        var secondIndex = differentBoxes ? 1 : 0;
        var results = await Task.WhenAll(Load(data), Load(data, secondIndex, byUrl: true));
        try
        {
            Assert.Contains(results, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.All(results, response => Assert.Contains(response.StatusCode,
                new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }));
        }
        finally { foreach (var response in results) response.Dispose(); }

        using var retryFirst = await Load(data);
        using var retrySecond = await Load(data, secondIndex);
        Assert.Equal(HttpStatusCode.NoContent, retryFirst.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, retrySecond.StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(differentBoxes ? 2 : 1, await db.BoxMovements.CountAsync(x => data.BoxIds.Contains(x.PhysicalBoxId)));
        Assert.Equal(differentBoxes ? 0 : 1,
            (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
    }

    [PointsPostgresFact]
    public async Task StockSaveFailureRollsBackBoxAndMovementToo()
    {
        var data = await Seed();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseApplicationServiceProvider(fixture.Factory.Services)
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new FailSecondSave())
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new BoxMovementService(db).LoadAsync(data.OrderId, data.UserId, Request(data)));
            Assert.Equal("Simulated stock save failure", exception.Message);
        }
        await AssertNotMoved(data);
    }

    private Task<HttpResponseMessage> Load(SeedData data, int boxIndex = 0, bool byUrl = false) =>
        fixture.Client.PostAsJsonAsync(Path(data), Request(data, boxIndex, byUrl));

    private static string Path(SeedData data) => $"/api/delivery-orders/{data.OrderId}/boxes/load";

    private static LoadBoxRequest Request(SeedData data, int boxIndex = 0, bool byUrl = false) => new()
    {
        OrderItemId = data.ItemIds[0],
        PhysicalBoxId = byUrl ? null : data.BoxIds[boxIndex],
        Url = byUrl ? $"  {data.Urls[boxIndex]}  " : null
    };

    private async Task AssertNotMoved(SeedData data)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var box = await db.PhysicalBoxes.FindAsync(data.BoxIds[0]);
        Assert.Equal(PhysicalBoxStatus.AtPoint, box!.Status);
        Assert.Equal(1, box.CurrentPointId);
        Assert.Null(box.CurrentOrderItemId);
        Assert.False(await db.BoxMovements.AnyAsync(x => x.PhysicalBoxId == box.Id));
        Assert.Equal(data.CompleteProducts,
            (await db.WarehouseProducts.SingleAsync(x => x.ProductId == data.ProductId)).Quantity);
    }

    private async Task<SeedData> Seed(
        int completeProducts = 1, int firstTypePerProduct = 1, bool twoTypes = false, string status = "loading")
    {
        var user = await fixture.Client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = new Product { Id = Interlocked.Increment(ref _productId), Name = "Loading test product" };
        db.Products.Add(product);
        var packaging = Enumerable.Range(0, twoTypes ? 2 : 1).Select(index => new Box
        {
            ProductId = product.Id, Name = $"Packaging {index}", QuantityPerProduct = index == 0 ? firstTypePerProduct : 1,
            LengthCm = 1, WidthCm = 1, HeightCm = 1, WeightKg = 1
        }).ToArray();
        db.Boxes.AddRange(packaging);
        var order = new DeliveryOrder { UserId = user!.Id, ResponsibleUserId = user.Id, SourcePointId = 1, Status = status };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        var items = packaging.Select(box => new DeliveryOrderItem
        {
            OrderId = order.Id, BoxId = box.Id, DestinationPointId = 2, Quantity = completeProducts * box.QuantityPerProduct
        }).ToArray();
        db.DeliveryOrderItems.AddRange(items);
        var physicalBoxes = packaging.SelectMany(box => Enumerable.Range(0, completeProducts * box.QuantityPerProduct)
            .Select(index => new PhysicalBox
            {
                BoxId = box.Id, Url = $"https://example.test/loading/{product.Id}/{box.Id}/{index}",
                CurrentPointId = 1, Status = PhysicalBoxStatus.AtPoint
            })).ToArray();
        db.PhysicalBoxes.AddRange(physicalBoxes);
        db.WarehouseProducts.Add(new WarehouseProduct { ProductId = product.Id, LogisticsPointId = 1, Quantity = completeProducts });
        await db.SaveChangesAsync();
        return new SeedData(order.Id, product.Id, user.Id, items.Select(x => x.Id).ToArray(),
            physicalBoxes.Select(x => x.Id).ToArray(), physicalBoxes.Select(x => x.Url).ToArray(), completeProducts);
    }

    private sealed record SeedData(long OrderId, long ProductId, string UserId,
        long[] ItemIds, long[] BoxIds, string[] Urls, int CompleteProducts);

    private sealed class FailSecondSave : SaveChangesInterceptor
    {
        private int _calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 2)
                throw new InvalidOperationException("Simulated stock save failure");
            return ValueTask.FromResult(result);
        }
    }
}
