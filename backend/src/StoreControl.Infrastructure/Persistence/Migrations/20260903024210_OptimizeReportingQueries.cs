using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReportingQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                schema: "sales",
                table: "SaleItems");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                schema: "sales",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductId" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Quantity", "LineSubtotal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                schema: "sales",
                table: "SaleItems");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                schema: "sales",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductId" },
                unique: true);
        }
    }
}
