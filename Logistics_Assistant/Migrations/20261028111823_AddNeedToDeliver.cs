using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Logistics_Assistant.Migrations
{
    /// <inheritdoc />
    public partial class AddNeedToDeliver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "need_to_deliver",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<long>(type: "bigint", nullable: true),
                    box_id = table.Column<long>(type: "bigint", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    logistic_point_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_need_to_deliver", x => x.id);
                    table.CheckConstraint("ck_need_to_deliver_product_or_box", "(product_id IS NOT NULL AND box_id IS NULL)\r\nOR\r\n(product_id IS NULL AND box_id IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "idx_need_to_deliver_logistic_point_id",
                table: "need_to_deliver",
                column: "logistic_point_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "need_to_deliver");
        }
    }
}
