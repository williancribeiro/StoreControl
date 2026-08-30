using StoreControl.Domain.Catalog;

namespace StoreControl.Domain.Purchasing;

public sealed class PurchaseReceiptItem
{
    private PurchaseReceiptItem()
    {
    }

    internal PurchaseReceiptItem(
        PurchaseReceipt receipt,
        PurchaseOrderItem purchaseOrderItem,
        decimal quantity)
    {
        Receipt = receipt;
        PurchaseOrderItem = purchaseOrderItem;
        PurchaseOrderItemId = purchaseOrderItem.Id;
        ProductId = purchaseOrderItem.ProductId;
        Quantity = quantity;
    }

    public Guid PurchaseReceiptId { get; private set; }

    public PurchaseReceipt Receipt { get; private set; } = null!;

    public long PurchaseOrderItemId { get; private set; }

    public PurchaseOrderItem PurchaseOrderItem { get; private set; } = null!;

    public int ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public decimal Quantity { get; private set; }
}
