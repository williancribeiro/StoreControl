namespace StoreControl.Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Created = 1,
    Pending = 2,
    PartiallyReceived = 3,
    Received = 4,
    Finalized = 5,
    Cancelled = 6
}
