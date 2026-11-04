using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Auth;
using Logistics_Assistant.DTOs.DeliveryOrders;
using Logistics_Assistant.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "DeliveryOrders")]
public sealed class DeliveryAssignmentApiTests(LogisticsPointsFixture fixture) : IClassFixture<LogisticsPointsFixture>
{
    [PointsPostgresFact]
    public async Task AssignmentUsesSignedInUserAndRemovesOrderFromUnassignedList()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(client);
        var me = await client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        var orderId = await CreateOrder();
        using var response = await client.PatchAsJsonAsync($"/api/delivery-orders/{orderId}/assign", new
        {
            userId = "cannot-impersonate-another-user"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var details = await client.GetFromJsonAsync<DeliveryOrderDetailsResponse>($"/api/delivery-orders/{orderId}");
        Assert.Equal(me!.Id, details!.Order.ResponsibleUserId);
        var unassigned = await client.GetFromJsonAsync<List<DeliveryOrder>>("/api/delivery-orders/unassigned");
        Assert.DoesNotContain(unassigned!, order => order.Id == orderId);
    }

    [PointsPostgresFact]
    public async Task ConcurrentAssignmentsHaveOneWinnerAndNeverOverwriteResponsibleUser()
    {
        using var first = AuthTestClient.Create(fixture.Factory);
        using var second = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(first);
        await AuthTestClient.Register(second);
        var firstUser = await first.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        var secondUser = await second.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        var orderId = await CreateOrder();
        var responses = await Task.WhenAll(
            first.PatchAsJsonAsync($"/api/delivery-orders/{orderId}/assign", new { }),
            second.PatchAsJsonAsync($"/api/delivery-orders/{orderId}/assign", new { }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winner = responses[0].IsSuccessStatusCode ? firstUser!.Id : secondUser!.Id;
            var details = await first.GetFromJsonAsync<DeliveryOrderDetailsResponse>($"/api/delivery-orders/{orderId}");
            Assert.Equal(winner, details!.Order.ResponsibleUserId);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private async Task<long> CreateOrder()
    {
        var creator = await fixture.Client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = new DeliveryOrder { UserId = creator!.Id };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
