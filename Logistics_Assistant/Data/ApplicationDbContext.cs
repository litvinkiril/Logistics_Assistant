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
        public DbSet<Box> Boxes { get; set; }
        public DbSet<PhysicalBox> PhysicalBoxes { get; set; }
        public DbSet<NeedToDeliver> NeedToDeliverItems { get; set; }
        public DbSet<DeliveryOrder> DeliveryOrders { get; set; }
        public DbSet<DeliveryOrderBox> DeliveryOrderBoxes { get; set; }
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

            modelBuilder.Entity<Box>()
                .HasOne(box => box.Product)
                .WithMany()
                .HasForeignKey(box => box.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PhysicalBox>()
                .HasOne(physicalBox => physicalBox.Box)
                .WithMany()
                .HasForeignKey(physicalBox => physicalBox.BoxId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NeedToDeliver>()
                .ToTable("need_to_deliver", table =>
                {
                    table.HasCheckConstraint(
                        "ck_need_to_deliver_product_or_box",
                        """
                        (product_id IS NOT NULL AND box_id IS NULL)
                        OR
                        (product_id IS NULL AND box_id IS NOT NULL)
                        """
                    );
                });
            modelBuilder.Entity<DeliveryOrderBox>(entity =>
            {
                entity.HasKey(x => new { x.OrderId, x.PhysicalBoxId });

                entity.Property(x => x.Status)
                    .HasDefaultValue(DeliveryBoxStatus.Loading);

                entity.ToTable("delivery_order_boxes", table =>
                {
                    table.HasCheckConstraint(
                        "ck_delivery_order_boxes_status",
                        "status IN (1, 2, 3, 4)"
                    );
                });
            });
        }
    }
}