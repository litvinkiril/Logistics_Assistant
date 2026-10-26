using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Logistics_Assistant.Models;

namespace Logistics_Assistant.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<LogisticsPoint> LogisticsPoints { get; set; }
        public DbSet<WarehouseProduct> WarehouseProducts { get; set; }

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WarehouseProduct>()
                .HasOne(stock => stock.LogisticsPoint)
                .WithMany()
                .HasForeignKey(stock => stock.LogisticsPointId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseProduct>()
                .HasOne(stock => stock.Product)
                .WithMany()
                .HasForeignKey(stock => stock.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}