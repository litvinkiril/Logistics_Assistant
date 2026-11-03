using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Tests;

internal static class TestCache
{
    // Real HybridCache, local tier only. Each test owns and disposes its cache.
    public static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }
}
