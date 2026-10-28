using Logistics_Assistant.Configuration;
using Logistics_Assistant.Services.Products;
using Logistics_Assistant.Services.Warehouses;



namespace Logistics_Assistant
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            DatabaseConfiguration.ConfigureDatabase(
                builder.Services,
                builder.Configuration);

            IdentityConfiguration.ConfigureIdentity(
                builder.Services);
            builder.Services.AddScoped<ProductSearchService>();
            builder.Services.AddScoped<WarehouseStockService>();
            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();
            app.MapControllers();

            app.Run();
        }
    }
}
