using StoreControl.Domain.Sales;

namespace StoreControl.UnitTests.Sales;

public sealed class SaleTests
{
    [Fact]
    public void Finalization_preserves_prices_and_calculates_multiple_items_payments_and_discount()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 2m, 12.345m);
        sale.AddItem(20, "PRD-020", "Leite", 3m, 4.5m);

        sale.FinalizeSale(
            3.21m,
            [
                (PaymentMethod.Cash, 20m, (string?)null),
                (PaymentMethod.Pix, 14.99m, "PIX-123")
            ]);

        Assert.Equal(SaleStatus.Finalized, sale.Status);
        Assert.Equal(38.20m, sale.Subtotal);
        Assert.Equal(3.21m, sale.DiscountAmount);
        Assert.Equal(34.99m, sale.TotalAmount);
        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal(12.35m, sale.Items.Single(item => item.ProductId == 10).UnitPrice);
        Assert.NotNull(sale.FinalizedAtUtc);
    }

    [Fact]
    public void Finalization_rejects_payment_difference_without_changing_sale_status()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 1m, 10m);

        var exception = Assert.Throws<InvalidOperationException>(() => sale.FinalizeSale(
            0m,
            [(PaymentMethod.Cash, 9.99m, (string?)null)]));

        Assert.Contains("pagamentos", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Empty(sale.Payments);
    }

    [Fact]
    public void Finalization_rejects_discount_equal_to_subtotal()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 1m, 10m);

        Assert.Throws<ArgumentOutOfRangeException>(() => sale.FinalizeSale(
            10m,
            [(PaymentMethod.Cash, 0.01m, (string?)null)]));
    }

    [Fact]
    public void Sale_does_not_accept_the_same_product_twice()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 1m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            sale.AddItem(10, "PRD-010", "Café", 2m, 10m));
    }

    [Fact]
    public void Cancellation_preserves_sale_values_and_records_audit_history()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 2m, 10m);
        sale.FinalizeSale(1m, [(PaymentMethod.Cash, 19m, (string?)null)]);
        var operationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        sale.Cancel(operationId, 20, userId, "  Cliente desistiu da compra.  ");

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(20m, sale.Subtotal);
        Assert.Equal(19m, sale.TotalAmount);
        Assert.Equal(operationId, sale.CancellationOperationId);
        Assert.Equal(userId, sale.CancelledByUserId);
        Assert.Equal("Cliente desistiu da compra.", sale.CancellationReason);
        Assert.Equal(2, sale.StatusHistory.Count);
        Assert.NotNull(sale.CancelledAtUtc);
    }

    [Fact]
    public void Cancellation_cannot_be_applied_twice()
    {
        var sale = NewSale();
        sale.AddItem(10, "PRD-010", "Café", 1m, 10m);
        sale.FinalizeSale(0m, [(PaymentMethod.Cash, 10m, (string?)null)]);
        sale.Cancel(Guid.NewGuid(), 1, Guid.NewGuid(), "Primeiro cancelamento.");

        Assert.Throws<InvalidOperationException>(() =>
            sale.Cancel(Guid.NewGuid(), 1, Guid.NewGuid(), "Segundo cancelamento."));
    }

    private static Sale NewSale() => new(Guid.NewGuid(), 1, 1, null, Guid.NewGuid());
}
