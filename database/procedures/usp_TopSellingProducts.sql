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
