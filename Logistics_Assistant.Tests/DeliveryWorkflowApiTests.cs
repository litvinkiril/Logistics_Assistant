using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Auth;
using Logistics_Assistant.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

public sealed class DeliveryWorkflowApiTests(LogisticsPointsFixture fixture) : IClassFixture<LogisticsPointsFixture>
{
    [PointsPostgresFact]
    public async Task MinePagesOnlyCurrentUsersOrdersAndRejectsNegativeOffset()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(client);
        var user = await client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.DeliveryOrders.AddRange(Enumerable.Range(1, 12).Select(id => new DeliveryOrder
        {
            Id = id, UserId = user!.Id, ResponsibleUserId = user.Id
        }));
        db.DeliveryOrders.Add(new DeliveryOrder { Id = 13, UserId = user!.Id });
        await db.SaveChangesAsync();

        var first = await client.GetFromJsonAsync<List<DeliveryOrder>>("/api/delivery-orders/mine");
        var second = await client.GetFromJsonAsync<List<DeliveryOrder>>("/api/delivery-orders/mine?offset=10");
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(10, first.Count);
        Assert.Equal(new long[] { 11, 12 }, second.Select(order => order.Id));
        Assert.All(first.Concat(second), order => Assert.Equal(user.Id, order.ResponsibleUserId));
        using var invalid = await client.GetAsync("/api/delivery-orders/mine?offset=-1");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [PointsPostgresFact]
    public async Task StatusRequiresResponsibleUserAndOrderedTransitions()
    {
        var user = await fixture.Client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = new DeliveryOrder { Id = 50, UserId = user!.Id, ResponsibleUserId = user.Id };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        var path = $"/api/delivery-orders/{order.Id}/status";
        using var skipped = await fixture.Client.PatchAsJsonAsync(path, new { status = "completed" });
        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        using var other = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(other);
        using var forbidden = await other.PatchAsJsonAsync(path, new { status = "loading" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        foreach (var status in new[] { "loading", "unloading", "completed" })
        {
            using var response = await fixture.Client.PatchAsJsonAsync(path, new { status });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
