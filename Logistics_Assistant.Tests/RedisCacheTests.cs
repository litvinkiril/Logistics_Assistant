using Logistics_Assistant.Configuration;
using Logistics_Assistant.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Logistics_Assistant.Tests;

[Trait("Feature", "Redis")]
public sealed class RedisCacheTests
{
    [RedisFact]
    public async Task FreshApplicationInstanceReadsSerializedProductsFromRedis()
    {
        var prefix = NewPrefix();
        const string key = "products:by-id:v2:42";
        await using var first = CreateServices(prefix);
        try
        {
            await first.GetRequiredService<HybridCache>().GetOrCreateAsync<List<Product>>(
                key, _ => ValueTask.FromResult(new List<Product>
                {
                    new() { Id = 42, Name = "Шкаф", Url = "https://example.test/42?a=1&b=2" }
                }));

            // A different provider has an empty local cache. Only Redis can supply this value.
            await using var second = CreateServices(prefix);
            var result = await second.GetRequiredService<HybridCache>().GetOrCreateAsync<List<Product>>(
                key, _ => throw new InvalidOperationException("Cache miss: Redis did not return the product."));

            var product = Assert.Single(result);
            Assert.Equal(42, product.Id);
            Assert.Equal("Шкаф", product.Name);
            Assert.Equal("https://example.test/42?a=1&b=2", product.Url);
        }
        finally
        {
            await first.GetRequiredService<IDistributedCache>().RemoveAsync(key);
        }
    }

    [RedisFact]
    public async Task RemovingKeyAlsoRemovesRedisEntry()
    {
        var prefix = NewPrefix();
        const string key = "remove-example";
        await using var first = CreateServices(prefix);
        try
        {
            var cache = first.GetRequiredService<HybridCache>();
            await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("old"));
            Assert.NotNull(await first.GetRequiredService<IDistributedCache>().GetAsync(key));

            await cache.RemoveAsync(key);
            Assert.Null(await first.GetRequiredService<IDistributedCache>().GetAsync(key));

            var factoryCalls = 0;
            await using var second = CreateServices(prefix);
            var value = await second.GetRequiredService<HybridCache>().GetOrCreateAsync(key, _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult("fresh");
            });
            Assert.Equal("fresh", value);
            Assert.Equal(1, factoryCalls);
        }
        finally
        {
            await first.GetRequiredService<IDistributedCache>().RemoveAsync(key);
        }
    }

    [RedisTheory]
    [InlineData(false, 5)]
    [InlineData(true, 10)]
    public async Task RedisEntryHasConfiguredExpiration(bool overrideOptions, int expectedMinutes)
    {
        var prefix = NewPrefix();
        const string key = "ttl-example";
        await using var services = CreateServices(prefix);
        try
        {
            await services.GetRequiredService<HybridCache>().GetOrCreateAsync(
                key, _ => ValueTask.FromResult("value"),
                options: overrideOptions ? new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    LocalCacheExpiration = TimeSpan.FromMinutes(1)
                } : null);

            using var connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
            var ttl = await connection.GetDatabase().KeyTimeToLiveAsync(prefix + key);
            Assert.NotNull(ttl); // Persistent or missing keys must fail this test.
            Assert.InRange(ttl.Value, TimeSpan.FromMinutes(expectedMinutes) - TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(expectedMinutes));
        }
        finally
        {
            await services.GetRequiredService<IDistributedCache>().RemoveAsync(key);
        }
    }

    private static string ConnectionString => Environment.GetEnvironmentVariable("LOGISTICS_TEST_REDIS")!;
    private static string NewPrefix() => $"Logistics:tests:{Guid.NewGuid():N}:";

    private static ServiceProvider CreateServices(string prefix)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = ConnectionString
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        CacheConfiguration.ConfigureCache(services, configuration);
        services.Configure<RedisCacheOptions>(options => options.InstanceName = prefix);
        return services.BuildServiceProvider();
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOGISTICS_TEST_REDIS")))
            Skip = "Run scripts/test-redis.ps1 against isolated Redis.";
    }
}

public sealed class RedisTheoryAttribute : TheoryAttribute
{
    public RedisTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOGISTICS_TEST_REDIS")))
            Skip = "Run scripts/test-redis.ps1 against isolated Redis.";
    }
}
