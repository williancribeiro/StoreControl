using StoreControl.Domain.Inventory;

namespace StoreControl.UnitTests.Inventory;

public sealed class StockTransferTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Transfer_follows_created_in_transit_received_lifecycle_with_history()
    {
        var transfer = NewTransfer();
        transfer.AddItem(10, "PRD-010", "Café", 2.3456m);

        transfer.Dispatch(Guid.NewGuid(), UserId);
        transfer.Receive(Guid.NewGuid(), UserId);

        Assert.Equal(StockTransferStatus.Received, transfer.Status);
        Assert.Equal(2.346m, transfer.Items.Single().Quantity);
        Assert.Equal(3, transfer.StatusHistory.Count);
        Assert.NotNull(transfer.DispatchedAtUtc);
        Assert.NotNull(transfer.ReceivedAtUtc);
    }

    [Fact]
    public void Transfer_rejects_equal_stores_and_duplicate_products()
    {
        Assert.Throws<ArgumentException>(() =>
            new StockTransfer(Guid.NewGuid(), 1, 1, UserId, null));
        var transfer = NewTransfer();
        transfer.AddItem(10, "PRD-010", "Café", 1m);

        Assert.Throws<InvalidOperationException>(() =>
            transfer.AddItem(10, "PRD-010", "Café", 1m));
    }

    [Fact]
    public void Transfer_can_only_be_cancelled_before_dispatch()
    {
        var cancelled = NewTransfer();
        cancelled.AddItem(10, "PRD-010", "Café", 1m);
        cancelled.Cancel(Guid.NewGuid(), UserId, "  Solicitação criada por engano.  ");

        Assert.Equal(StockTransferStatus.Cancelled, cancelled.Status);
        Assert.Equal("Solicitação criada por engano.", cancelled.CancellationReason);
        Assert.Equal(2, cancelled.StatusHistory.Count);

        var dispatched = NewTransfer();
        dispatched.AddItem(10, "PRD-010", "Café", 1m);
        dispatched.Dispatch(Guid.NewGuid(), UserId);
        Assert.Throws<InvalidOperationException>(() =>
            dispatched.Cancel(Guid.NewGuid(), UserId, "Tarde demais."));
    }

    [Fact]
    public void Transfer_cannot_be_received_before_dispatch_or_dispatched_twice()
    {
        var transfer = NewTransfer();
        transfer.AddItem(10, "PRD-010", "Café", 1m);

        Assert.Throws<InvalidOperationException>(() => transfer.Receive(Guid.NewGuid(), UserId));
        transfer.Dispatch(Guid.NewGuid(), UserId);
        Assert.Throws<InvalidOperationException>(() => transfer.Dispatch(Guid.NewGuid(), UserId));
    }

    private static StockTransfer NewTransfer() =>
        new(Guid.NewGuid(), 1, 2, UserId, "Reposição da filial.");
}
