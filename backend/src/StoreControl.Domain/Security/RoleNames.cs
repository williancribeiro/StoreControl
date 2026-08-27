namespace StoreControl.Domain.Security;

public static class RoleNames
{
    public const string Administrator = "Administrador";
    public const string Manager = "Gerente";
    public const string Seller = "Vendedor";
    public const string StockClerk = "Estoquista";

    public static readonly IReadOnlyCollection<string> All =
    [
        Administrator,
        Manager,
        Seller,
        StockClerk
    ];
}
