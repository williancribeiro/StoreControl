using StoreControl.Domain.Catalog;

namespace StoreControl.UnitTests.Catalog;

public sealed class CategoryTests
{
    [Fact]
    public void Constructor_normalizes_master_data()
    {
        var category = new Category("  higiene  ", "  Higiene pessoal ", "  Cuidados diários  ");

        Assert.Equal("HIGIENE", category.Code);
        Assert.Equal("Higiene pessoal", category.Name);
        Assert.Equal("Cuidados diários", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Update_can_change_active_state()
    {
        var category = new Category("UTIL", "Utilidades", null);

        category.Update("UTIL", "Utilidades domésticas", null, false);

        Assert.False(category.IsActive);
        Assert.Equal("Utilidades domésticas", category.Name);
    }
}
