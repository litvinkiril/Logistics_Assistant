using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logistics_Assistant.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPlanningAndBoxTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stop on ambiguous legacy data; never merge/delete boxes automatically.
            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM physical_boxes GROUP BY url HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate physical_boxes.url: resolve duplicate labels before migration.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM products WHERE url IS NOT NULL GROUP BY url HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate products.url: resolve product URL duplicates before migration.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM delivery_order_boxes WHERE status <> 4
                               GROUP BY physical_box_id HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'A physical box belongs to multiple active orders: reconcile assignments before migration.';
                    END IF;
                END;
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_warehouse_products_logistics_points_id",
                table: "warehouse_products");

            migrationBuilder.DropIndex(
                name: "idx_products_url",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_physical_boxes_url",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_box_id",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_boxes_product_id",
                table: "boxes");

            migrationBuilder.AddColumn<long>(
                name: "current_order_id",
                table: "physical_boxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "current_point_id",
                table: "physical_boxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "product_id",
                table: "physical_boxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "product_unit_id",
                table: "physical_boxes",
                type: "bigint",
                nullable: true);

            // Product is known from packaging; unit and location cannot be inferred.
            migrationBuilder.Sql("""
                UPDATE physical_boxes AS physical
                SET product_id = packaging.product_id
                FROM boxes AS packaging
                WHERE packaging.id = physical.box_id;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "max_load_kg",
                table: "delivery_orders",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");

            migrationBuilder.AlterColumn<string>(
                name: "contact_phone",
                table: "delivery_orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_width_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_length_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_height_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)");

            migrationBuilder.AlterColumn<string>(
                name: "car_plate",
                table: "delivery_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<long>(
                name: "source_point_id",
                table: "delivery_orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "delivery_order_boxes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "loaded_at",
                table: "delivery_order_boxes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "order_need_id",
                table: "delivery_order_boxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "unloaded_at",
                table: "delivery_order_boxes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_boxes_id_product_id",
                table: "boxes",
                columns: new[] { "id", "product_id" });

            migrationBuilder.CreateTable(
                name: "box_movements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    physical_box_id = table.Column<long>(type: "bigint", nullable: false),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    from_point_id = table.Column<long>(type: "bigint", nullable: true),
                    to_point_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_box_movements", x => x.id);
                    table.CheckConstraint("ck_box_movements_direction", "(from_point_id IS NOT NULL AND to_point_id IS NULL) OR (from_point_id IS NULL AND to_point_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_box_movements_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_box_movements_delivery_order_boxes_order_id_physical_box_id",
                        columns: x => new { x.order_id, x.physical_box_id },
                        principalTable: "delivery_order_boxes",
                        principalColumns: new[] { "order_id", "physical_box_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_box_movements_logistics_points_from_point_id",
                        column: x => x.from_point_id,
                        principalTable: "logistics_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_box_movements_logistics_points_to_point_id",
                        column: x => x.to_point_id,
                        principalTable: "logistics_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_order_needs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    need_to_deliver_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    delivered_quantity = table.Column<int>(type: "integer", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_order_needs", x => x.id);
                    table.UniqueConstraint("AK_delivery_order_needs_id_order_id", x => new { x.id, x.order_id });
                    table.CheckConstraint("ck_delivery_order_needs_quantity", "quantity > 0 AND delivered_quantity >= 0 AND delivered_quantity <= quantity");
                    table.ForeignKey(
                        name: "FK_delivery_order_needs_delivery_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "delivery_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_order_needs_need_to_deliver_need_to_deliver_id",
                        column: x => x.need_to_deliver_id,
                        principalTable: "need_to_deliver",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_units",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    expected_box_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_units", x => x.id);
                    table.UniqueConstraint("AK_product_units_id_product_id", x => new { x.id, x.product_id });
                    table.CheckConstraint("ck_product_units_expected_boxes", "expected_box_count > 0");
                    table.ForeignKey(
                        name: "FK_product_units_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_order_units",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    order_need_id = table.Column<long>(type: "bigint", nullable: false),
                    product_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_order_units", x => x.id);
                    table.CheckConstraint("ck_delivery_order_units_release", "delivered_at IS NULL OR (released_at IS NOT NULL AND released_at >= delivered_at)");
                    table.ForeignKey(
                        name: "FK_delivery_order_units_delivery_order_needs_order_need_id_ord~",
                        columns: x => new { x.order_need_id, x.order_id },
                        principalTable: "delivery_order_needs",
                        principalColumns: new[] { "id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_order_units_product_units_product_unit_id",
                        column: x => x.product_unit_id,
                        principalTable: "product_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_products_logistics_points_id_product_id",
                table: "warehouse_products",
                columns: new[] { "logistics_points_id", "product_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_products_quantity",
                table: "warehouse_products",
                sql: "quantity >= 0");

            migrationBuilder.CreateIndex(
                name: "idx_products_url",
                table: "products",
                column: "url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_physical_boxes_url",
                table: "physical_boxes",
                column: "url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_box_id_product_id",
                table: "physical_boxes",
                columns: new[] { "box_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_current_order_id",
                table: "physical_boxes",
                column: "current_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_current_point_id",
                table: "physical_boxes",
                column: "current_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_product_unit_id_box_id",
                table: "physical_boxes",
                columns: new[] { "product_unit_id", "box_id" },
                unique: true,
                filter: "product_unit_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_product_unit_id_product_id",
                table: "physical_boxes",
                columns: new[] { "product_unit_id", "product_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_physical_boxes_location",
                table: "physical_boxes",
                sql: "current_point_id IS NULL OR current_order_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_physical_boxes_unit_product",
                table: "physical_boxes",
                sql: "product_unit_id IS NULL OR product_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_physical_boxes_url",
                table: "physical_boxes",
                sql: "length(btrim(url)) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_need_to_deliver_box_id",
                table: "need_to_deliver",
                column: "box_id");

            migrationBuilder.CreateIndex(
                name: "IX_need_to_deliver_product_id",
                table: "need_to_deliver",
                column: "product_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_need_to_deliver_quantity",
                table: "need_to_deliver",
                sql: "quantity IS NULL OR quantity > 0");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_source_point_id",
                table: "delivery_orders",
                column: "source_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_user_id",
                table: "delivery_orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_boxes_order_need_id_order_id",
                table: "delivery_order_boxes",
                columns: new[] { "order_need_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_boxes_physical_box_id",
                table: "delivery_order_boxes",
                column: "physical_box_id",
                unique: true,
                filter: "status <> 4 AND cancelled_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_delivery_order_boxes_scan_times",
                table: "delivery_order_boxes",
                sql: "unloaded_at IS NULL OR (loaded_at IS NOT NULL AND unloaded_at >= loaded_at)");

            migrationBuilder.CreateIndex(
                name: "IX_boxes_product_id_box_number",
                table: "boxes",
                columns: new[] { "product_id", "box_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_boxes_dimensions",
                table: "boxes",
                sql: "width_cm > 0 AND height_cm > 0 AND length_cm > 0 AND weight_kg > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_boxes_number",
                table: "boxes",
                sql: "box_number > 0");

            migrationBuilder.CreateIndex(
                name: "IX_box_movements_from_point_id",
                table: "box_movements",
                column: "from_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_box_movements_order_id_physical_box_id",
                table: "box_movements",
                columns: new[] { "order_id", "physical_box_id" });

            migrationBuilder.CreateIndex(
                name: "IX_box_movements_physical_box_id_occurred_at",
                table: "box_movements",
                columns: new[] { "physical_box_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_box_movements_to_point_id",
                table: "box_movements",
                column: "to_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_box_movements_user_id",
                table: "box_movements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_needs_need_to_deliver_id",
                table: "delivery_order_needs",
                column: "need_to_deliver_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_needs_order_id_need_to_deliver_id",
                table: "delivery_order_needs",
                columns: new[] { "order_id", "need_to_deliver_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_units_order_id_product_unit_id",
                table: "delivery_order_units",
                columns: new[] { "order_id", "product_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_units_order_need_id_order_id",
                table: "delivery_order_units",
                columns: new[] { "order_need_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_order_units_product_unit_id",
                table: "delivery_order_units",
                column: "product_unit_id",
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_units_product_id",
                table: "product_units",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_order_boxes_delivery_order_needs_order_need_id_ord~",
                table: "delivery_order_boxes",
                columns: new[] { "order_need_id", "order_id" },
                principalTable: "delivery_order_needs",
                principalColumns: new[] { "id", "order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_order_boxes_delivery_orders_order_id",
                table: "delivery_order_boxes",
                column: "order_id",
                principalTable: "delivery_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_order_boxes_logistics_points_point_id",
                table: "delivery_order_boxes",
                column: "point_id",
                principalTable: "logistics_points",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_order_boxes_physical_boxes_physical_box_id",
                table: "delivery_order_boxes",
                column: "physical_box_id",
                principalTable: "physical_boxes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_orders_AspNetUsers_user_id",
                table: "delivery_orders",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_orders_logistics_points_source_point_id",
                table: "delivery_orders",
                column: "source_point_id",
                principalTable: "logistics_points",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_need_to_deliver_boxes_box_id",
                table: "need_to_deliver",
                column: "box_id",
                principalTable: "boxes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_need_to_deliver_logistics_points_logistic_point_id",
                table: "need_to_deliver",
                column: "logistic_point_id",
                principalTable: "logistics_points",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_need_to_deliver_products_product_id",
                table: "need_to_deliver",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_physical_boxes_boxes_box_id_product_id",
                table: "physical_boxes",
                columns: new[] { "box_id", "product_id" },
                principalTable: "boxes",
                principalColumns: new[] { "id", "product_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_physical_boxes_delivery_orders_current_order_id",
                table: "physical_boxes",
                column: "current_order_id",
                principalTable: "delivery_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_physical_boxes_logistics_points_current_point_id",
                table: "physical_boxes",
                column: "current_point_id",
                principalTable: "logistics_points",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_physical_boxes_product_units_product_unit_id_product_id",
                table: "physical_boxes",
                columns: new[] { "product_unit_id", "product_id" },
                principalTable: "product_units",
                principalColumns: new[] { "id", "product_id" },
                onDelete: ReferentialAction.Restrict);

            // A separate read model: existing aggregate stock is kept until reconciliation.
            migrationBuilder.Sql("""
                CREATE VIEW logistics_unit_stock AS
                WITH complete_units AS (
                    SELECT unit.id, unit.product_id, min(physical.current_point_id) AS point_id
                    FROM product_units AS unit
                    JOIN physical_boxes AS physical ON physical.product_unit_id = unit.id
                    GROUP BY unit.id, unit.product_id, unit.expected_box_count
                    HAVING count(*) = unit.expected_box_count
                       AND count(physical.current_point_id) = unit.expected_box_count
                       AND min(physical.current_point_id) = max(physical.current_point_id)
                ), unit_availability AS (
                    SELECT unit.*,
                        EXISTS (SELECT 1 FROM delivery_order_units reservation
                                WHERE reservation.product_unit_id = unit.id
                                  AND reservation.released_at IS NULL)
                        OR EXISTS (SELECT 1 FROM physical_boxes physical
                                   JOIN delivery_order_boxes assignment ON assignment.physical_box_id = physical.id
                                   WHERE physical.product_unit_id = unit.id
                                     AND assignment.status <> 4 AND assignment.cancelled_at IS NULL)
                        AS reserved
                    FROM complete_units unit
                )
                SELECT point_id AS logistics_point_id, product_id,
                       count(*) AS quantity,
                       count(*) FILTER (WHERE reserved) AS reserved_quantity,
                       count(*) FILTER (WHERE NOT reserved) AS available_quantity
                FROM unit_availability
                GROUP BY point_id, product_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback must not silently erase reservations or movement history.
            migrationBuilder.Sql("""
                DO $rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM product_units)
                       OR EXISTS (SELECT 1 FROM delivery_order_needs)
                       OR EXISTS (SELECT 1 FROM delivery_order_units)
                       OR EXISTS (SELECT 1 FROM box_movements)
                       OR EXISTS (SELECT 1 FROM physical_boxes WHERE product_unit_id IS NOT NULL
                                  OR current_point_id IS NOT NULL OR current_order_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM delivery_orders WHERE source_point_id IS NOT NULL
                                  OR car_plate IS NULL OR contact_phone IS NULL OR cargo_length_cm IS NULL
                                  OR cargo_width_cm IS NULL OR cargo_height_cm IS NULL OR max_load_kg IS NULL)
                       OR EXISTS (SELECT 1 FROM delivery_order_boxes WHERE order_need_id IS NOT NULL
                                  OR loaded_at IS NOT NULL OR unloaded_at IS NOT NULL OR cancelled_at IS NOT NULL)
                    THEN
                        RAISE EXCEPTION 'Rollback would lose logistics data. Prepare an explicit data-preserving rollback.';
                    END IF;
                END;
                $rollback$;
                """);

            migrationBuilder.Sql("DROP VIEW logistics_unit_stock;");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_order_boxes_delivery_order_needs_order_need_id_ord~",
                table: "delivery_order_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_order_boxes_delivery_orders_order_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_order_boxes_logistics_points_point_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_order_boxes_physical_boxes_physical_box_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_orders_AspNetUsers_user_id",
                table: "delivery_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_delivery_orders_logistics_points_source_point_id",
                table: "delivery_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_need_to_deliver_boxes_box_id",
                table: "need_to_deliver");

            migrationBuilder.DropForeignKey(
                name: "FK_need_to_deliver_logistics_points_logistic_point_id",
                table: "need_to_deliver");

            migrationBuilder.DropForeignKey(
                name: "FK_need_to_deliver_products_product_id",
                table: "need_to_deliver");

            migrationBuilder.DropForeignKey(
                name: "FK_physical_boxes_boxes_box_id_product_id",
                table: "physical_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_physical_boxes_delivery_orders_current_order_id",
                table: "physical_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_physical_boxes_logistics_points_current_point_id",
                table: "physical_boxes");

            migrationBuilder.DropForeignKey(
                name: "FK_physical_boxes_product_units_product_unit_id_product_id",
                table: "physical_boxes");

            migrationBuilder.DropTable(
                name: "box_movements");

            migrationBuilder.DropTable(
                name: "delivery_order_units");

            migrationBuilder.DropTable(
                name: "delivery_order_needs");

            migrationBuilder.DropTable(
                name: "product_units");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_products_logistics_points_id_product_id",
                table: "warehouse_products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_products_quantity",
                table: "warehouse_products");

            migrationBuilder.DropIndex(
                name: "idx_products_url",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_physical_boxes_url",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_box_id_product_id",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_current_order_id",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_current_point_id",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_product_unit_id_box_id",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_physical_boxes_product_unit_id_product_id",
                table: "physical_boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_physical_boxes_location",
                table: "physical_boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_physical_boxes_unit_product",
                table: "physical_boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_physical_boxes_url",
                table: "physical_boxes");

            migrationBuilder.DropIndex(
                name: "IX_need_to_deliver_box_id",
                table: "need_to_deliver");

            migrationBuilder.DropIndex(
                name: "IX_need_to_deliver_product_id",
                table: "need_to_deliver");

            migrationBuilder.DropCheckConstraint(
                name: "ck_need_to_deliver_quantity",
                table: "need_to_deliver");

            migrationBuilder.DropIndex(
                name: "IX_delivery_orders_source_point_id",
                table: "delivery_orders");

            migrationBuilder.DropIndex(
                name: "IX_delivery_orders_user_id",
                table: "delivery_orders");

            migrationBuilder.DropIndex(
                name: "IX_delivery_order_boxes_order_need_id_order_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropIndex(
                name: "IX_delivery_order_boxes_physical_box_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_delivery_order_boxes_scan_times",
                table: "delivery_order_boxes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_boxes_id_product_id",
                table: "boxes");

            migrationBuilder.DropIndex(
                name: "IX_boxes_product_id_box_number",
                table: "boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_boxes_dimensions",
                table: "boxes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_boxes_number",
                table: "boxes");

            migrationBuilder.DropColumn(
                name: "current_order_id",
                table: "physical_boxes");

            migrationBuilder.DropColumn(
                name: "current_point_id",
                table: "physical_boxes");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "physical_boxes");

            migrationBuilder.DropColumn(
                name: "product_unit_id",
                table: "physical_boxes");

            migrationBuilder.DropColumn(
                name: "source_point_id",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "delivery_order_boxes");

            migrationBuilder.DropColumn(
                name: "loaded_at",
                table: "delivery_order_boxes");

            migrationBuilder.DropColumn(
                name: "order_need_id",
                table: "delivery_order_boxes");

            migrationBuilder.DropColumn(
                name: "unloaded_at",
                table: "delivery_order_boxes");

            migrationBuilder.AlterColumn<decimal>(
                name: "max_load_kg",
                table: "delivery_orders",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "contact_phone",
                table: "delivery_orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_width_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_length_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "cargo_height_cm",
                table: "delivery_orders",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "car_plate",
                table: "delivery_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_products_logistics_points_id",
                table: "warehouse_products",
                column: "logistics_points_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_url",
                table: "products",
                column: "url");

            migrationBuilder.CreateIndex(
                name: "idx_physical_boxes_url",
                table: "physical_boxes",
                column: "url");

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_box_id",
                table: "physical_boxes",
                column: "box_id");

            migrationBuilder.CreateIndex(
                name: "IX_boxes_product_id",
                table: "boxes",
                column: "product_id");
        }
    }
}
