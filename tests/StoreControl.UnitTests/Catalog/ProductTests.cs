using StoreControl.Domain.Catalog;

namespace StoreControl.UnitTests.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Constructor_normalizes_and_rounds_product_values()
    {
        var product = new Product(
            "  prd-1 ",
            "7891234567890",
            "  Produto teste  ",
            "  descrição  ",
            1,
            10.12345m,
            15.125m);

        Assert.Equal("PRD-1", product.Code);
        Assert.Equal("Produto teste", product.Name);
        Assert.Equal(10.1235m, product.CostPrice);
        Assert.Equal(15.13m, product.SalePrice);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("78912345A7890")]
    public void Constructor_rejects_invalid_barcode(string barcode)
    {
        Assert.Throws<ArgumentException>(() => new Product(
            "PRD-1",
            barcode,
            "Produto teste",
            null,
            1,
            10m,
            15m));
    }

    [Fact]
    public void Constructor_rejects_negative_money()
    {
        Assert.Throws<ArgumentException>(() => new Product(
            "PRD-1",
            null,
            "Produto teste",
            null,
            1,
            -0.01m,
            15m));
    }
}
