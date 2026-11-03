using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Configuration
{
    public static class CacheConfiguration
    {
        public static void ConfigureCache(
            IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException(
                    "Не задана строка подключения ConnectionStrings:Redis.");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = "Logistics:";
            });

            services.AddHybridCache(options =>
            {
                options.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(5),
                    LocalCacheExpiration = TimeSpan.FromSeconds(30)
                };
            });
        }
    }
}