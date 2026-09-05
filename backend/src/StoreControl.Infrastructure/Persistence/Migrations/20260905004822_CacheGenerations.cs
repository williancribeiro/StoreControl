using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CacheGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CacheRevisions",
                schema: "platform",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheRevisions", x => x.Name);
                });

            migrationBuilder.Sql("INSERT INTO [platform].[CacheRevisions] ([Name]) VALUES (N'dashboard'), (N'categories');");

            foreach (var (schema, table, region) in Dependencies)
            {
                // Identifiers are migration-owned constants, never request input.
                var triggerSql = $"""
                    CREATE OR ALTER TRIGGER [{schema}].[TR_{table}_CacheRevision]
                    ON [{schema}].[{table}]
                    AFTER INSERT, UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;
                        IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                            RETURN;
                        UPDATE [platform].[CacheRevisions]
                        SET [Generation] = NEWID()
                        WHERE [Name] = N'{region}';
                        IF @@ROWCOUNT <> 1
                            THROW 51002, 'Required cache generation is missing.', 1;
                    END;
                    """;
                // EXEC gives CREATE TRIGGER its own batch in idempotent scripts as well as EF execution.
                migrationBuilder.Sql("EXEC(N'" + triggerSql.Replace("'", "''", StringComparison.Ordinal) + "');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table, _) in Dependencies)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS [{schema}].[TR_{table}_CacheRevision];");
            }

            migrationBuilder.DropTable(
                name: "CacheRevisions",
                schema: "platform");
        }

        private static readonly (string Schema, string Table, string Region)[] Dependencies =
        [
            ("catalog", "Categories", "categories"),
            ("catalog", "Products", "dashboard"),
            ("organization", "Stores", "dashboard"),
            ("inventory", "StockBalances", "dashboard"),
            ("sales", "Sales", "dashboard"),
            ("sales", "SaleItems", "dashboard")
        ];
    }
}
