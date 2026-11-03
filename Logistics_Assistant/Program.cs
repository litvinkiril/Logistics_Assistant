using Logistics_Assistant.Configuration;
using Logistics_Assistant.Services.Products;
using Logistics_Assistant.Services.LogisticsPoints;



namespace Logistics_Assistant
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            CacheConfiguration.ConfigureCache(
                builder.Services,
                builder.Configuration);

            DatabaseConfiguration.ConfigureDatabase(
                builder.Services,
                builder.Configuration);

            IdentityConfiguration.ConfigureIdentity(
                builder.Services);
                builder.Services.AddScoped<ProductSearchService>();
                builder.Services.AddScoped<LogisticsPointService>();
                builder.Services.AddControllers();
                builder.Services.AddProblemDetails();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllers();

            app.Run();
        }
    }
}
