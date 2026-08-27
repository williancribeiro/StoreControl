using StoreControl.Domain.Parties;

namespace StoreControl.UnitTests.Parties;

public sealed class PartyTests
{
    [Fact]
    public void Customer_normalizes_document_and_email()
    {
        var customer = new Customer(
            " cli-1 ",
            " Cliente teste ",
            "123.456.789-01",
            " CLIENTE@EXAMPLE.COM ",
            "(11) 99999-0000");

        Assert.Equal("CLI-1", customer.Code);
        Assert.Equal("12345678901", customer.Document);
        Assert.Equal("cliente@example.com", customer.Email);
    }

    [Fact]
    public void Supplier_rejects_malformed_document()
    {
        Assert.Throws<ArgumentException>(() => new Supplier(
            "FOR-1",
            "Fornecedor teste",
            "1234",
            null,
            null));
    }

    [Fact]
    public void Customer_rejects_letters_hidden_in_document_formatting()
    {
        Assert.Throws<ArgumentException>(() => new Customer(
            "CLI-1",
            "Cliente teste",
            "ABC12345678901",
            null,
            null));
    }

    [Fact]
    public void Supplier_can_be_inactivated_and_reactivated()
    {
        var supplier = new Supplier("FOR-1", "Fornecedor teste", null, null, null);

        supplier.Update("FOR-1", "Fornecedor teste", null, null, null, false);
        Assert.False(supplier.IsActive);

        supplier.Update("FOR-1", "Fornecedor teste", null, null, null, true);
        Assert.True(supplier.IsActive);
    }
}
