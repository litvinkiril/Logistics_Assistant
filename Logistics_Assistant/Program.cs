using Logistics_Assistant.Configuration;
using Logistics_Assistant.Services.Products;


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
            // Add services to the container.
            builder.Services.AddRazorPages();

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

            app.Run();
        }
    }
}
