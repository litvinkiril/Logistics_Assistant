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
            entity.HasIndex(x => new { x.LogisticsPointId, x.ProductId }).IsUnique();
            entity.ToTable("warehouse_products", table =>
                table.HasCheckConstraint("ck_warehouse_products_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<Box>(entity =>
        {
            entity.HasAlternateKey(x => new { x.Id, x.ProductId });
            entity.HasIndex(x => new { x.ProductId, x.BoxNumber }).IsUnique();
            entity.ToTable("boxes", table =>
            {
                table.HasCheckConstraint("ck_boxes_number", "box_number > 0");
                table.HasCheckConstraint("ck_boxes_dimensions",
                    "width_cm > 0 AND height_cm > 0 AND length_cm > 0 AND weight_kg > 0");
            });
        });

        modelBuilder.Entity<NeedToDeliver>(entity =>
        {
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Box>().WithMany().HasForeignKey(x => x.BoxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.LogisticPointId)
                .OnDelete(DeleteBehavior.Restrict);
            // Legacy incomplete needs remain readable; allocation requires complete values.
            entity.ToTable("need_to_deliver", table =>
                table.HasCheckConstraint("ck_need_to_deliver_quantity", "quantity IS NULL OR quantity > 0"));
        });

        modelBuilder.Entity<DeliveryOrder>(entity =>
        {
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.SourcePointId)
                .OnDelete(DeleteBehavior.Restrict);
            // Historical zero values are not silently changed in this migration.
            // Starting loading must validate nonempty vehicle details and positive dimensions.
        });

        modelBuilder.Entity<ProductUnit>(entity =>
        {
            entity.HasAlternateKey(x => new { x.Id, x.ProductId });
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("product_units", table =>
                table.HasCheckConstraint("ck_product_units_expected_boxes", "expected_box_count > 0"));
        });

        modelBuilder.Entity<PhysicalBox>(entity =>
        {
            // Composite keys ensure that both packaging and unit belong to the same product.
            entity.HasOne<Box>().WithMany().HasForeignKey(x => new { x.BoxId, x.ProductId })
                .HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductUnit>().WithMany().HasForeignKey(x => new { x.ProductUnitId, x.ProductId })
                .HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.CurrentPointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryOrder>().WithMany().HasForeignKey(x => x.CurrentOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ProductUnitId, x.BoxId }).IsUnique()
                .HasFilter("product_unit_id IS NOT NULL");
            entity.ToTable("physical_boxes", table =>
            {
                table.HasCheckConstraint("ck_physical_boxes_url", "length(btrim(url)) > 0");
                table.HasCheckConstraint("ck_physical_boxes_unit_product",
                    "product_unit_id IS NULL OR product_id IS NOT NULL");
                // Both NULL = legacy/unknown location, never interpreted as available stock.
                table.HasCheckConstraint("ck_physical_boxes_location",
                    "current_point_id IS NULL OR current_order_id IS NULL");
            });
        });

        modelBuilder.Entity<DeliveryOrderNeed>(entity =>
        {
            entity.HasAlternateKey(x => new { x.Id, x.OrderId });
            entity.HasIndex(x => new { x.OrderId, x.NeedToDeliverId }).IsUnique();
            entity.HasOne<DeliveryOrder>().WithMany().HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NeedToDeliver>().WithMany().HasForeignKey(x => x.NeedToDeliverId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("delivery_order_needs", table =>
                table.HasCheckConstraint("ck_delivery_order_needs_quantity",
                    "quantity > 0 AND delivered_quantity >= 0 AND delivered_quantity <= quantity"));
        });

        modelBuilder.Entity<DeliveryOrderUnit>(entity =>
        {
            entity.HasOne<DeliveryOrderNeed>().WithMany()
                .HasForeignKey(x => new { x.OrderNeedId, x.OrderId })
                .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductUnit>().WithMany().HasForeignKey(x => x.ProductUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrderId, x.ProductUnitId }).IsUnique();
            entity.HasIndex(x => x.ProductUnitId).IsUnique().HasFilter("released_at IS NULL");
            entity.ToTable("delivery_order_units", table =>
                table.HasCheckConstraint("ck_delivery_order_units_release",
                    "delivered_at IS NULL OR (released_at IS NOT NULL AND released_at >= delivered_at)"));
        });

        modelBuilder.Entity<DeliveryOrderBox>(entity =>
        {
            entity.HasOne<DeliveryOrder>().WithMany().HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PhysicalBox>().WithMany().HasForeignKey(x => x.PhysicalBoxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.PointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryOrderNeed>().WithMany()
                .HasForeignKey(x => new { x.OrderNeedId, x.OrderId })
                .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PhysicalBoxId).IsUnique()
                .HasFilter("status <> 4 AND cancelled_at IS NULL");
            entity.ToTable("delivery_order_boxes", table =>
                table.HasCheckConstraint("ck_delivery_order_boxes_scan_times",
                    "unloaded_at IS NULL OR (loaded_at IS NOT NULL AND unloaded_at >= loaded_at)"));
        });

        modelBuilder.Entity<BoxMovement>(entity =>
        {
            entity.HasOne<DeliveryOrderBox>().WithMany()
                .HasForeignKey(x => new { x.OrderId, x.PhysicalBoxId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.FromPointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsPoint>().WithMany().HasForeignKey(x => x.ToPointId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PhysicalBoxId, x.OccurredAt });
            entity.ToTable("box_movements", table =>
                table.HasCheckConstraint("ck_box_movements_direction",
                    "(from_point_id IS NOT NULL AND to_point_id IS NULL) OR " +
                    "(from_point_id IS NULL AND to_point_id IS NOT NULL)"));
        });
    }
}
