using Logistics_Assistant.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics_Assistant.Configuration
{
    public static class IdentityConfiguration
    {
        public static void ConfigureIdentity(
            IServiceCollection services)
        {
            services
                .AddDefaultIdentity<IdentityUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedAccount = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>();
        }
    }
}