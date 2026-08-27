using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "FoundationRecords",
                schema: "platform",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundationRecords", x => x.Key);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "FoundationRecords",
                columns: new[] { "Key", "UpdatedAtUtc", "Value" },
                values: new object[] { "foundation-version", new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), "1" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoundationRecords",
                schema: "platform");
        }
    }
}
