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
