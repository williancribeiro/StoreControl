using StoreControl.Domain.Inventory;

namespace StoreControl.UnitTests.Inventory;

public sealed class StockBalanceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Movement_updates_balance_and_preserves_explanatory_values()
    {
        var balance = new StockBalance(1, 2, 5.5555m);

        var entry = balance.ApplyMovement(
            StockMovementType.PositiveAdjustment,
            10.1234m,
            " Contagem inicial ",
            UserId,
            "unit-test");
        var exit = balance.ApplyMovement(
            StockMovementType.NegativeAdjustment,
            2.123m,
            "Avaria identificada",
            UserId,
            "unit-test");

        Assert.Equal(5.556m, balance.MinimumQuantity);
        Assert.Equal(8m, balance.QuantityOnHand);
        Assert.Equal(0m, entry.QuantityBefore);
        Assert.Equal(10.123m, entry.QuantityAfter);
        Assert.Equal(10.123m, entry.QuantityChange);
        Assert.Equal("Contagem inicial", entry.Reason);
        Assert.Equal(10.123m, exit.QuantityBefore);
        Assert.Equal(8m, exit.QuantityAfter);
        Assert.Equal(-2.123m, exit.QuantityChange);
    }

    [Fact]
    public void Negative_movement_rejects_insufficient_stock_without_changing_balance()
    {
        var balance = new StockBalance(1, 2);
        balance.ApplyMovement(
            StockMovementType.PositiveAdjustment,
            1m,
            "Entrada",
            UserId,
            "unit-test");

        Assert.Throws<InvalidOperationException>(() => balance.ApplyMovement(
            StockMovementType.NegativeAdjustment,
            1.001m,
            "Saída inválida",
            UserId,
            "unit-test"));

        Assert.Equal(1m, balance.QuantityOnHand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0004)]
    public void Movement_rejects_non_positive_quantity_after_rounding(decimal quantity)
    {
        var balance = new StockBalance(1, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => balance.ApplyMovement(
            StockMovementType.PositiveAdjustment,
            quantity,
            "Quantidade inválida",
            UserId,
            "unit-test"));
    }

    [Fact]
    public void Balance_identifies_low_stock_only_when_minimum_is_configured()
    {
        var withoutMinimum = new StockBalance(1, 2);
        var withMinimum = new StockBalance(1, 3, 4m);
        withMinimum.ApplyMovement(
            StockMovementType.InitialBalance,
            4m,
            "Saldo inicial",
            UserId,
            "unit-test");

        Assert.False(withoutMinimum.IsBelowMinimum);
        Assert.True(withMinimum.IsBelowMinimum);
    }
}
