using Logistics_Assistant.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Logistics_Assistant.Configuration
{
    public static class DatabaseConfiguration
    {
        public static void ConfigureDatabase(
            IServiceCollection services,
            IConfiguration settings)
        {
            var connectionString =
                settings.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(
                options =>
                {
                    options.UseNpgsql(connectionString);
                });
        }
    }
}
