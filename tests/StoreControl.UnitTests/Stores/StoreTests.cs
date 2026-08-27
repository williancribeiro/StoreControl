using StoreControl.Domain.Stores;

namespace StoreControl.UnitTests.Stores;

public sealed class StoreTests
{
    [Fact]
    public void Constructor_normalizes_code_and_name()
    {
        var store = new Store(" central ", " Loja Central ");

        Assert.Equal("CENTRAL", store.Code);
        Assert.Equal("Loja Central", store.Name);
        Assert.True(store.IsActive);
    }

    [Fact]
    public void Update_changes_state_and_preserves_normalization()
    {
        var store = new Store("central", "Loja Central");

        store.Update(" norte ", " Loja Norte ", false);

        Assert.Equal("NORTE", store.Code);
        Assert.Equal("Loja Norte", store.Name);
        Assert.False(store.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_empty_code(string code)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Store(code, "Loja"));

        Assert.Equal("code", exception.ParamName);
    }
}
