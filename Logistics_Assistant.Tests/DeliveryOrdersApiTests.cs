using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "DeliveryOrders")]
public class DeliveryOrdersApiTests
    : IClassFixture<LogisticsPointsFixture>
{
    private readonly LogisticsPointsFixture _fixture;

    public DeliveryOrdersApiTests(LogisticsPointsFixture fixture)
    {
        _fixture = fixture;
    }

    [PointsPostgresFact]
    public async Task GetUnassignedAppliesOffsetAndReturnsTenOrders()
    {
        const string userId = "delivery-orders-test-user";

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = userId
            });

            db.DeliveryOrders.AddRange(
                Enumerable.Range(1, 15).Select(id => new DeliveryOrder
                {
                    Id = id,
                    UserId = userId,
                    ResponsibleUserId = id == 2 ? userId : null,
                    Status = "new"
                }));

            await db.SaveChangesAsync();
        }

        using var response = await _fixture.Client.GetAsync(
            "/api/delivery-orders/unassigned?offset=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content
            .ReadFromJsonAsync<List<DeliveryOrder>>();

        Assert.NotNull(orders);
        Assert.Equal(10, orders.Count);
        Assert.All(orders, order =>
            Assert.Null(order.ResponsibleUserId));

        var expectedIds = Enumerable.Range(4, 10)
            .Select(id => (long)id)
            .ToArray();

        Assert.Equal(
            expectedIds,
            orders.Select(order => order.Id).ToArray());
    }
}