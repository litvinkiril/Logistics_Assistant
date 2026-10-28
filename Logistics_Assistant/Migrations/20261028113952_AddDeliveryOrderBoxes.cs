using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics_Assistant.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOrderBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_order_boxes",
                columns: table => new
                {
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    physical_box_id = table.Column<long>(type: "bigint", nullable: false),
                    point_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_order_boxes", x => new { x.order_id, x.physical_box_id });
                    table.CheckConstraint("ck_delivery_order_boxes_status", "status IN (1, 2, 3, 4)");
                });

            migrationBuilder.CreateIndex(
                name: "idx_delivery_order_boxes_point_id",
                table: "delivery_order_boxes",
                column: "point_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_order_boxes");
        }
    }
}
