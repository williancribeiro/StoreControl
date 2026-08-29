namespace StoreControl.Domain.Inventory;

public enum StockMovementType
{
    PurchaseReceipt = 1,
    Sale = 2,
    PositiveAdjustment = 3,
    NegativeAdjustment = 4,
    TransferIn = 5,
    TransferOut = 6,
    SaleReversal = 7,
    OtherIn = 8,
    OtherOut = 9,
    InitialBalance = 10
}
