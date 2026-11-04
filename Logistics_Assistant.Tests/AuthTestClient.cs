using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Logistics_Assistant.Tests;

internal static class AuthTestClient
{
    internal const string Password = "test-password-2468";

    internal static HttpClient Create(WebApplicationFactory<Program> factory) => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    internal static async Task RefreshCsrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.False(string.IsNullOrWhiteSpace(token?.Token));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
    }

    internal static async Task<string> Register(HttpClient client)
    {
        var email = $"auth-{Guid.NewGuid():N}@example.test";
        await RefreshCsrf(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await RefreshCsrf(client);
        return email;
    }

    private sealed record CsrfResponse(string Token);
}
