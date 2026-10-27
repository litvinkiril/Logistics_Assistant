using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logistics_Assistant.Migrations
{
    /// <inheritdoc />
    public partial class AddBoxesAndPhysicalBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boxes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    box_number = table.Column<int>(type: "integer", nullable: false),
                    width_cm = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    height_cm = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    length_cm = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,3)", nullable: false),
                    is_fragile = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_boxes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "physical_boxes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    box_id = table.Column<long>(type: "bigint", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_physical_boxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_physical_boxes_boxes_box_id",
                        column: x => x.box_id,
                        principalTable: "boxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_boxes_product_id",
                table: "boxes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_physical_boxes_url",
                table: "physical_boxes",
                column: "url");

            migrationBuilder.CreateIndex(
                name: "IX_physical_boxes_box_id",
                table: "physical_boxes",
                column: "box_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "physical_boxes");

            migrationBuilder.DropTable(
                name: "boxes");
        }
    }
}
