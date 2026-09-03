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
