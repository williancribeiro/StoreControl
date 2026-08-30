using StoreControl.Domain.Cash;
using StoreControl.Domain.Sales;

namespace StoreControl.UnitTests.Cash;

public sealed class CashSessionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Session_tracks_only_cash_payments_in_expected_cash_amount()
    {
        var session = NewSession(100m);
        var sale = NewFinalizedSale(
            (PaymentMethod.Cash, 30m, (string?)null),
            (PaymentMethod.CreditCard, 20m, "AUTH-1"));

        foreach (var payment in sale.Payments)
        {
            session.RecordSalePayment(payment, UserId, "unit-test", sale.OperationId);
        }

        Assert.Equal(130m, session.ExpectedCashAmount);
        Assert.Equal(3, session.Movements.Count);
    }

    [Fact]
    public void Manual_entries_and_exits_use_a_signed_ledger()
    {
        var session = NewSession(50m);

        var cashIn = session.RecordManualMovement(
            Guid.NewGuid(), CashMovementType.CashIn, 20m, "Reforço", UserId, "unit-test");
        var cashOut = session.RecordManualMovement(
            Guid.NewGuid(), CashMovementType.CashOut, 15m, "Retirada", UserId, "unit-test");

        Assert.Equal(20m, cashIn.Amount);
        Assert.Equal(-15m, cashOut.Amount);
        Assert.Equal(55m, session.ExpectedCashAmount);
    }

    [Fact]
    public void Cash_exit_cannot_make_expected_amount_negative()
    {
        var session = NewSession(10m);

        Assert.Throws<InvalidOperationException>(() => session.RecordManualMovement(
            Guid.NewGuid(), CashMovementType.CashOut, 10.01m, "Retirada", UserId, "unit-test"));

        Assert.Equal(10m, session.ExpectedCashAmount);
        Assert.Single(session.Movements);
    }

    [Fact]
    public void Closing_records_declared_difference_and_blocks_new_movements()
    {
        var session = NewSession(100m);

        session.Close(UserId, 98.5m);

        Assert.Equal(CashSessionStatus.Closed, session.Status);
        Assert.Equal(-1.5m, session.DifferenceAmount);
        Assert.Throws<InvalidOperationException>(() => session.RecordManualMovement(
            Guid.NewGuid(), CashMovementType.CashIn, 1m, "Tardia", UserId, "unit-test"));
    }

    [Fact]
    public void Sale_refund_reverses_only_the_physical_cash_payment()
    {
        var session = NewSession(100m);
        var sale = NewFinalizedSale(
            (PaymentMethod.Cash, 30m, (string?)null),
            (PaymentMethod.Pix, 20m, "PIX-1"));
        foreach (var payment in sale.Payments)
        {
            session.RecordSalePayment(payment, UserId, "unit-test", sale.OperationId);
        }

        var cancellationId = Guid.NewGuid();
        foreach (var payment in sale.Payments)
        {
            session.RecordSaleRefund(payment, UserId, "unit-test", cancellationId);
        }

        Assert.Equal(100m, session.ExpectedCashAmount);
        Assert.Equal(5, session.Movements.Count);
        Assert.All(
            session.Movements.Where(item => item.Type == CashMovementType.SaleRefund),
            item => Assert.True(item.Amount < 0));
    }

    private static CashSession NewSession(decimal openingAmount) =>
        new(Guid.NewGuid(), 1, UserId, openingAmount, "unit-test");

    private static Sale NewFinalizedSale(
        params (PaymentMethod Method, decimal Amount, string? Reference)[] payments)
    {
        var sale = new Sale(Guid.NewGuid(), 1, 1, null, UserId);
        sale.AddItem(1, "PRD-001", "Produto", 1m, payments.Sum(payment => payment.Amount));
        sale.FinalizeSale(0m, payments);
        return sale;
    }
}
