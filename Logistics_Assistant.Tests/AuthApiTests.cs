using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.Data;
using Logistics_Assistant.DTOs.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "Auth")]
public sealed class AuthApiTests(LogisticsPointsFixture fixture) : IClassFixture<LogisticsPointsFixture>
{
    [PointsPostgresTheory]
    [InlineData("/api/products/1")]
    [InlineData("/api/logistics-points")]
    [InlineData("/api/delivery-orders/unassigned")]
    [InlineData("/api/delivery-orders/1")]
    [InlineData("/api/auth/me")]
    public async Task AnonymousApiRequestsReturn401WithoutRedirect(string path)
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [PointsPostgresFact]
    public async Task RegistrationSignsInImmediatelyWithoutConfirmingEmail()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        var email = await AuthTestClient.Register(client);
        var me = await client.GetFromJsonAsync<AuthUserResponse>("/api/auth/me");
        Assert.Equal(email, me!.Email);
        using var scope = fixture.Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.FindAsync(me.Id);
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual(AuthTestClient.Password, user.PasswordHash);
        using var products = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, products.StatusCode);
    }

    [PointsPostgresFact]
    public async Task LoginPersistsCookieAndLogoutRemovesAccess()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        var email = await AuthTestClient.Register(client);
        using var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var anonymous = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await AuthTestClient.RefreshCsrf(client);
        using var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = AuthTestClient.Password, rememberMe = true });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("Logistics.Auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        using var authenticated = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    [PointsPostgresTheory]
    [InlineData("register")]
    [InlineData("login")]
    public async Task AuthMutationsWithoutCsrfAreRejected(string action)
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        using var response = await client.PostAsJsonAsync($"/api/auth/{action}", new
        {
            email = "csrf@example.test", password = AuthTestClient.Password
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PointsPostgresFact]
    public async Task LogoutAndAssignmentRequireCsrfEvenWhenSignedIn()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.Register(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        using var assign = await client.PatchAsJsonAsync("/api/delivery-orders/1/assign", new { });
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        using var session = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [PointsPostgresFact]
    public async Task DuplicateEmailIsRejectedCaseInsensitively()
    {
        using var first = AuthTestClient.Create(fixture.Factory);
        var email = await AuthTestClient.Register(first);
        using var second = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.RefreshCsrf(second);
        using var response = await second.PostAsJsonAsync("/api/auth/register", new
        {
            email = email.ToUpperInvariant(), password = AuthTestClient.Password
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var me = await second.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [PointsPostgresTheory]
    [InlineData("invalid", "long-password")]
    [InlineData("valid@example.test", "short")]
    public async Task InvalidCredentialsAreRejectedOnRegistration(string email, string password)
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        await AuthTestClient.RefreshCsrf(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PointsPostgresFact]
    public async Task FiveWrongPasswordsTemporarilyLockTheAccount()
    {
        using var client = AuthTestClient.Create(fixture.Factory);
        var email = await AuthTestClient.Register(client);
        using var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AuthTestClient.RefreshCsrf(client);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(attempt == 5 ? HttpStatusCode.TooManyRequests : HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
