using Logistics_Assistant.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Logistics_Assistant.Data;

internal static class LogisticsModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarehouseProduct>(entity =>
        {
            entity.HasOne(x => x.LogisticsPoint).WithMany().HasForeignKey(x => x.LogisticsPointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.LogisticsPointId, x.ProductId }).IsUnique();
            entity.ToTable("warehouse_products", table =>
                table.HasCheckConstraint("ck_warehouse_products_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<Box>(entity =>
        {
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
            entity.Property(x => x.QuantityPerProduct).HasDefaultValue(1);
            entity.ToTable("boxes", table =>
            {
                table.HasCheckConstraint("ck_boxes_name", "length(btrim(name)) > 0");
                table.HasCheckConstraint("ck_boxes_quantity_per_product", "quantity_per_product > 0");
                table.HasCheckConstraint("ck_boxes_dimensions",
                    "width_cm > 0 AND height_cm > 0 AND length_cm > 0 AND weight_kg > 0");
            });
        });

        modelBuilder.Entity<DeliveryOrder>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ResponsibleUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.SourcePointId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryOrderItem>(entity =>
        {
            entity.HasAlternateKey(x => new { x.Id, x.BoxId });
            entity.HasOne<DeliveryOrder>().WithMany().HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Box>().WithMany().HasForeignKey(x => x.BoxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.DestinationPointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrderId, x.BoxId, x.DestinationPointId }).IsUnique();
            entity.ToTable("delivery_order_items", table =>
                table.HasCheckConstraint("ck_delivery_order_items_quantity", "quantity > 0"));
        });

        modelBuilder.Entity<PhysicalBox>(entity =>
        {
            entity.HasOne(x => x.Box).WithMany().HasForeignKey(x => x.BoxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.CurrentPointId)
                .OnDelete(DeleteBehavior.Restrict);
            // The assigned item must request this packaging type.
            entity.HasOne<DeliveryOrderItem>().WithMany()
                .HasForeignKey(x => new { x.CurrentOrderItemId, x.BoxId })
                .HasPrincipalKey(x => new { x.Id, x.BoxId }).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Status).HasDefaultValue(PhysicalBoxStatus.Unknown);
            entity.ToTable("physical_boxes", table =>
            {
                table.HasCheckConstraint("ck_physical_boxes_url", "length(btrim(url)) > 0");
                // Assignment and location coexist while the box is reserved at a point.
                table.HasCheckConstraint("ck_physical_boxes_location", """
                    (status = 0 AND current_point_id IS NULL)
                    OR (status = 1 AND current_point_id IS NOT NULL)
                    OR (status = 2 AND current_point_id IS NULL AND current_order_item_id IS NOT NULL)
                    """);
            });
        });

        modelBuilder.Entity<BoxMovement>(entity =>
        {
            entity.HasOne<PhysicalBox>().WithMany().HasForeignKey(x => x.PhysicalBoxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryOrderItem>().WithMany().HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.PointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PhysicalBoxId, x.OccurredAt });
            entity.ToTable("box_movements", table =>
                table.HasCheckConstraint("ck_box_movements_operation", "operation IN (1, 2)"));
        });
    }
}
