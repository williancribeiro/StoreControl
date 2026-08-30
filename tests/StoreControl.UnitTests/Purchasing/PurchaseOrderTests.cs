using StoreControl.Domain.Purchasing;

namespace StoreControl.UnitTests.Purchasing;

public sealed class PurchaseOrderTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Creation_preserves_product_snapshot_and_rounds_financial_totals()
    {
        var order = CreateOrder();

        order.AddItem(10, " prod-10 ", " Produto histórico ", 2.3456m, 10.12345m);

        var item = Assert.Single(order.Items);
        Assert.Equal(PurchaseOrderStatus.Created, order.Status);
        Assert.Equal("prod-10", item.ProductCode);
        Assert.Equal("Produto histórico", item.ProductName);
        Assert.Equal(2.346m, item.OrderedQuantity);
        Assert.Equal(10.1235m, item.UnitCost);
        Assert.Equal(23.75m, item.LineTotal);
        Assert.Equal(item.LineTotal, order.TotalAmount);
        Assert.Single(order.StatusHistory);
    }

    [Fact]
    public void Submit_requires_items_and_only_created_order_can_be_submitted()
    {
        var emptyOrder = CreateOrder();
        Assert.Throws<InvalidOperationException>(() => emptyOrder.Submit(UserId));

        var order = CreateOrderWithOneItem();
        order.Submit(UserId);

        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.Submit(UserId));
    }

    [Fact]
    public void Partial_and_complete_receipts_update_quantities_and_status()
    {
        var order = CreateOrderWithOneItem(5m);
        order.Submit(UserId);
        var item = Assert.Single(order.Items);

        order.Receive(Guid.NewGuid(), [(item.Id, 2m)], UserId, "Primeira entrega");
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(2m, item.ReceivedQuantity);
        Assert.Equal(3m, item.RemainingQuantity);

        order.Receive(Guid.NewGuid(), [(item.Id, 3m)], UserId, "Entrega final");
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(5m, item.ReceivedQuantity);
        Assert.Equal(2, order.Receipts.Count);

        order.Finalize(UserId);
        Assert.Equal(PurchaseOrderStatus.Finalized, order.Status);
    }

    [Fact]
    public void Receipt_rejects_duplicate_identifier_and_quantity_above_remaining()
    {
        var order = CreateOrderWithOneItem(4m);
        order.Submit(UserId);
        var item = Assert.Single(order.Items);
        var receiptId = Guid.NewGuid();

        order.Receive(receiptId, [(item.Id, 1m)], UserId, null);

        Assert.Throws<InvalidOperationException>(() =>
            order.Receive(receiptId, [(item.Id, 1m)], UserId, null));
        Assert.Throws<InvalidOperationException>(() =>
            order.Receive(Guid.NewGuid(), [(item.Id, 4m)], UserId, null));
        Assert.Equal(1m, item.ReceivedQuantity);
    }

    [Fact]
    public void Cancellation_is_allowed_before_receipt_and_forbidden_after_receipt()
    {
        var cancellable = CreateOrderWithOneItem();
        cancellable.Submit(UserId);
        cancellable.Cancel(UserId, "Fornecedor indisponível");
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancellable.Status);

        var received = CreateOrderWithOneItem(2m);
        received.Submit(UserId);
        var item = Assert.Single(received.Items);
        received.Receive(Guid.NewGuid(), [(item.Id, 1m)], UserId, null);

        Assert.Throws<InvalidOperationException>(() => received.Cancel(UserId, "Tarde demais"));
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, received.Status);
    }

    [Fact]
    public void Created_order_cannot_receive_or_finalize()
    {
        var order = CreateOrderWithOneItem();
        var item = Assert.Single(order.Items);

        Assert.Throws<InvalidOperationException>(() =>
            order.Receive(Guid.NewGuid(), [(item.Id, 1m)], UserId, null));
        Assert.Throws<InvalidOperationException>(() => order.Finalize(UserId));
        Assert.Equal(PurchaseOrderStatus.Created, order.Status);
    }

    private static PurchaseOrder CreateOrder() => new(1, 2, UserId, null, "Teste");

    private static PurchaseOrder CreateOrderWithOneItem(decimal quantity = 3m)
    {
        var order = CreateOrder();
        order.AddItem(10, "PRD-10", "Produto", quantity, 12.50m);
        return order;
    }
}
