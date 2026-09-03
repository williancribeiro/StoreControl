using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OccurredAtUtc_StoreId_MovementType",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "OccurredAtUtc", "StoreId", "MovementType" },
                descending: new[] { true, false, false })
                .Annotation("SqlServer:Include", new[] { "QuantityChange" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status_FinalizedAtUtc_StoreId",
                schema: "sales",
                table: "Sales",
                columns: new[] { "Status", "FinalizedAtUtc", "StoreId" },
                descending: new[] { false, true, false })
                .Annotation("SqlServer:Include", new[] { "Subtotal", "DiscountAmount", "TotalAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedAtUtc_StoreId_Status",
                schema: "purchasing",
                table: "PurchaseOrders",
                columns: new[] { "CreatedAtUtc", "StoreId", "Status" },
                descending: new[] { true, false, false })
                .Annotation("SqlServer:Include", new[] { "TotalAmount" });

            migrationBuilder.Sql(
                """
                CREATE OR ALTER VIEW [reporting].[vw_DailySales]
                AS
                    SELECT
                        [sale].[StoreId],
                        CONVERT(date, [sale].[FinalizedAtUtc]) AS [SaleDate],
                        COUNT_BIG(*) AS [SaleCount],
                        SUM([sale].[Subtotal]) AS [GrossRevenue],
                        SUM([sale].[DiscountAmount]) AS [DiscountAmount],
                        SUM([sale].[TotalAmount]) AS [Revenue]
                    FROM [sales].[Sales] AS [sale]
                    WHERE [sale].[Status] = 2
                    GROUP BY [sale].[StoreId], CONVERT(date, [sale].[FinalizedAtUtc]);
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER VIEW [reporting].[vw_StockPosition]
                AS
                    SELECT
                        [balance].[StoreId],
                        [store].[Code] AS [StoreCode],
                        [store].[Name] AS [StoreName],
                        [balance].[ProductId],
                        [product].[Code] AS [ProductCode],
                        [product].[Name] AS [ProductName],
                        [category].[Name] AS [CategoryName],
                        [balance].[QuantityOnHand],
                        [balance].[MinimumQuantity],
                        CONVERT(bit, CASE
                            WHEN [balance].[MinimumQuantity] > 0
                                AND [balance].[QuantityOnHand] <= [balance].[MinimumQuantity]
                            THEN 1 ELSE 0 END) AS [IsBelowMinimum]
                    FROM [inventory].[StockBalances] AS [balance]
                    INNER JOIN [organization].[Stores] AS [store] ON [store].[Id] = [balance].[StoreId]
                    INNER JOIN [catalog].[Products] AS [product] ON [product].[Id] = [balance].[ProductId]
                    INNER JOIN [catalog].[Categories] AS [category] ON [category].[Id] = [product].[CategoryId];
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [reporting].[usp_TopSellingProducts]
                    @FromUtc datetime2(3),
                    @ToUtcExclusive datetime2(3),
                    @StoreIds nvarchar(max),
                    @Limit int
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @FromUtc >= @ToUtcExclusive
                        THROW 51000, 'The reporting period is invalid.', 1;

                    IF @Limit < 1 OR @Limit > 100
                        THROW 51001, 'The result limit must be between 1 and 100.', 1;

                    SELECT TOP (@Limit)
                        [item].[ProductId],
                        MAX([product].[Code]) AS [ProductCode],
                        MAX([product].[Name]) AS [ProductName],
                        SUM([item].[Quantity]) AS [QuantitySold],
                        SUM([item].[LineSubtotal]) AS [GrossRevenue],
                        COUNT_BIG(*) AS [SaleCount]
                    FROM [sales].[SaleItems] AS [item]
                    INNER JOIN [sales].[Sales] AS [sale] ON [sale].[Id] = [item].[SaleId]
                    INNER JOIN [catalog].[Products] AS [product] ON [product].[Id] = [item].[ProductId]
                    WHERE [sale].[Status] = 2
                        AND [sale].[FinalizedAtUtc] >= @FromUtc
                        AND [sale].[FinalizedAtUtc] < @ToUtcExclusive
                        AND [sale].[StoreId] IN
                        (
                            SELECT DISTINCT TRY_CONVERT(int, [value])
                            FROM STRING_SPLIT(@StoreIds, ',')
                            WHERE TRY_CONVERT(int, [value]) IS NOT NULL
                        )
                    GROUP BY [item].[ProductId]
                    ORDER BY [QuantitySold] DESC, [GrossRevenue] DESC, [item].[ProductId];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [reporting].[usp_TopSellingProducts];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [reporting].[vw_StockPosition];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [reporting].[vw_DailySales];");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OccurredAtUtc_StoreId_MovementType",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Status_FinalizedAtUtc_StoreId",
                schema: "sales",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_CreatedAtUtc_StoreId_Status",
                schema: "purchasing",
                table: "PurchaseOrders");
        }
    }
}
