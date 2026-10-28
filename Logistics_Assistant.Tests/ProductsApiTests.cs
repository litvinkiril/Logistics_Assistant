using System.Net;
using System.Net.Http.Json;
using Logistics_Assistant.DTOs.Products;
using Logistics_Assistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "Products")]
public sealed class ProductsApiTests : IClassFixture<LogisticsPointsFixture>
{
    private readonly LogisticsPointsFixture _fixture;
    public ProductsApiTests(LogisticsPointsFixture fixture) => _fixture = fixture;

    [PointsPostgresFact]
    public async Task ProductByIdPreservesSingleItemArrayResponse()
    {
        using var response = await _fixture.Client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();
        var product = Assert.Single(products!);
        Assert.Equal(1, product.Id);
        Assert.Equal("Тестовый шкаф", product.Name);
    }

    [PointsPostgresTheory]
    [InlineData("/api/products/999999", HttpStatusCode.NotFound)]
    [InlineData("/api/products/999999/stock", HttpStatusCode.NotFound)]
    [InlineData("/api/products/0", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/-1/stock", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/abc", HttpStatusCode.NotFound)]
    [InlineData("/api/products/9223372036854775808", HttpStatusCode.NotFound)]
    [InlineData("/api/products/search", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/search?name=%20%20", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/search?name=test&offset=-1", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/search?name=test&offset=abc", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/search?name=test&offset=2147483648", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/by-url", HttpStatusCode.BadRequest)]
    [InlineData("/api/products/by-url?url=%20%20", HttpStatusCode.BadRequest)]
    public async Task InvalidRequestsReturnExpectedStatus(string url, HttpStatusCode status)
    {
        using var response = await _fixture.Client.GetAsync(url);
        Assert.Equal(status, response.StatusCode);
    }

    [PointsPostgresTheory]
    [InlineData(0, new long[] { 1, 2 })]
    [InlineData(1, new long[] { 2 })]
    [InlineData(20, new long[] { })]
    public async Task NameSearchPreservesOrderingAndOffset(int offset, long[] expected)
    {
        var name = Uri.EscapeDataString(" тЕсТоВыЙ ");
        var products = await _fixture.Client.GetFromJsonAsync<List<Product>>($"/api/products/search?name={name}&offset={offset}");
        Assert.Equal(expected, products!.Select(product => product.Id));
    }

    [PointsPostgresFact]
    public async Task UrlSearchAcceptsEncodedQueryStringAndReturnsEmptyArrayWhenMissing()
    {
        var url = Uri.EscapeDataString(" https://example.test/products/cabinet?ref=one&part=two ");
        var products = await _fixture.Client.GetFromJsonAsync<List<Product>>($"/api/products/by-url?url={url}");
        Assert.Equal(1, Assert.Single(products!).Id);
        Assert.Empty((await _fixture.Client.GetFromJsonAsync<List<Product>>("/api/products/by-url?url=missing"))!);
    }

    [PointsPostgresFact]
    public async Task StockReturnsWarehouseAndPickupPointWithoutChangingEnvelope()
    {
        var response = await _fixture.Client.GetFromJsonAsync<ProductStockResponse>("/api/products/1/stock");
        Assert.NotNull(response);
        Assert.Equal(1, response.Product.Id);
        Assert.Equal(new long[] { 1, 2 }, response.Warehouses.Select(stock => stock.Warehouse.Id));
        Assert.Equal(new[] { 12, 3 }, response.Warehouses.Select(stock => stock.Quantity));
        Assert.Empty((await _fixture.Client.GetFromJsonAsync<ProductStockResponse>("/api/products/3/stock"))!.Warehouses);
    }

    [PointsPostgresTheory]
    [InlineData("/Products/Search?handler=ById&idStr=1")]
    [InlineData("/Privacy")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    public async Task RemovedRazorRoutesAreNotRegistered(string url)
    {
        using var response = await _fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PointsPostgresFact]
    public async Task RootIsJsonAndApplicationContainsNoPageActions()
    {
        using var response = await _fixture.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var actions = _fixture.Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        Assert.DoesNotContain(actions.ActionDescriptors.Items, action => action.GetType().Name.Contains("PageActionDescriptor"));
    }

    [PointsPostgresTheory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task UnexpectedErrorsAreHandledAsJsonForAnyVerb(string verb)
    {
        await using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(ApiTestProbeController).Assembly)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(new HttpMethod(verb), "/__tests/exception");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-only-private-error", body);
        Assert.DoesNotContain("<html", body);
    }

    [PointsPostgresFact]
    public async Task UnauthorizedApiDoesNotRedirectToRemovedLoginPage()
    {
        await using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(ApiTestProbeController).Assembly)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/__tests/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}

// Included only by the test factories above, never by the production application.
[ApiController]
public sealed class ApiTestProbeController : ControllerBase
{
    [Route("/__tests/exception")]
    public IActionResult Throw() => throw new InvalidOperationException("test-only-private-error");

    [Authorize]
    [HttpGet("/__tests/protected")]
    public IActionResult Protected() => Ok();
}
