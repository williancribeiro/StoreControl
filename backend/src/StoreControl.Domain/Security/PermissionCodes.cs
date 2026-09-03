namespace StoreControl.Domain.Security;

public static class PermissionCodes
{
    public const string StoresRead = "stores.read";
    public const string StoresManage = "stores.manage";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";
    public const string CategoriesRead = "categories.read";
    public const string CategoriesManage = "categories.manage";
    public const string ProductsRead = "products.read";
    public const string ProductsManage = "products.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersManage = "customers.manage";
    public const string SuppliersRead = "suppliers.read";
    public const string SuppliersManage = "suppliers.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryManage = "inventory.manage";
    public const string PurchasesRead = "purchases.read";
    public const string PurchasesManage = "purchases.manage";
    public const string SalesRead = "sales.read";
    public const string SalesManage = "sales.manage";
    public const string CashRead = "cash.read";
    public const string CashManage = "cash.manage";
    public const string ReportsRead = "reports.read";

    public static readonly IReadOnlyCollection<string> All =
    [
        StoresRead,
        StoresManage,
        UsersRead,
        UsersManage,
        RolesRead,
        RolesManage,
        CategoriesRead,
        CategoriesManage,
        ProductsRead,
        ProductsManage,
        CustomersRead,
        CustomersManage,
        SuppliersRead,
        SuppliersManage,
        InventoryRead,
        InventoryManage,
        PurchasesRead,
        PurchasesManage,
        SalesRead,
        SalesManage,
        CashRead,
        CashManage,
        ReportsRead
    ];
}
