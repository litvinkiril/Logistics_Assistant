using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Logistics_Assistant.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOrderResponsibleUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "responsible_user_id",
                table: "delivery_orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_orders_responsible_user_id",
                table: "delivery_orders",
                column: "responsible_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_orders_AspNetUsers_responsible_user_id",
                table: "delivery_orders",
                column: "responsible_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_orders_AspNetUsers_responsible_user_id",
                table: "delivery_orders");

            migrationBuilder.DropIndex(
                name: "IX_delivery_orders_responsible_user_id",
                table: "delivery_orders");

            migrationBuilder.DropColumn(
                name: "responsible_user_id",
                table: "delivery_orders");
        }
    }
}
