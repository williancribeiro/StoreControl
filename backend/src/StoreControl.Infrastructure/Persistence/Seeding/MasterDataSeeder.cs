using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Domain.Catalog;
using StoreControl.Domain.Parties;

namespace StoreControl.Infrastructure.Persistence.Seeding;

internal sealed class MasterDataSeeder(
    StoreControlDbContext dbContext,
    ILogger<MasterDataSeeder> logger)
{
    private static readonly (string Code, string Name, string Description)[] CategoryDefaults =
    [
        ("ALIMENTOS", "Alimentos", "Mercearia, alimentos secos e itens básicos."),
        ("BEBIDAS", "Bebidas", "Bebidas não alcoólicas e itens relacionados."),
        ("HIGIENE", "Higiene pessoal", "Cuidados pessoais e higiene."),
        ("LIMPEZA", "Limpeza", "Produtos para limpeza doméstica e profissional."),
        ("PAPELARIA", "Papelaria", "Materiais de escritório e escolares."),
        ("ELETRONICOS", "Eletrônicos", "Acessórios e pequenos eletrônicos."),
        ("UTILIDADES", "Utilidades domésticas", "Itens de uso diário para a casa."),
        ("PET", "Pet", "Alimentos e cuidados para animais domésticos.")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var categories = await EnsureCategoriesAsync(cancellationToken);
        await EnsureProductsAsync(categories, cancellationToken);
        await EnsureCustomersAsync(cancellationToken);
        await EnsureSuppliersAsync(cancellationToken);

        logger.LogInformation(
            "StoreControl master-data demonstration seed is ready with {CategoryCount} categories and at least {ProductCount} products",
            CategoryDefaults.Length,
            120);
    }

    private async Task<IReadOnlyDictionary<string, Category>> EnsureCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Categories
            .Select(category => category.Code)
            .ToListAsync(cancellationToken);
        dbContext.Categories.AddRange(CategoryDefaults
            .Where(item => !existingCodes.Contains(item.Code, StringComparer.Ordinal))
            .Select(item => new Category(item.Code, item.Name, item.Description)));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Categories.ToDictionaryAsync(category => category.Code, cancellationToken);
    }

    private async Task EnsureProductsAsync(
        IReadOnlyDictionary<string, Category> categories,
        CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Products
            .Select(product => product.Code)
            .ToListAsync(cancellationToken);
        var categoryArray = CategoryDefaults.Select(item => categories[item.Code]).ToArray();
        var products = Enumerable.Range(1, 120)
            .Select(number =>
            {
                var category = categoryArray[(number - 1) % categoryArray.Length];
                var code = $"PRD-{number:0000}";
                var cost = decimal.Round(2.75m + number * 1.13m, 4, MidpointRounding.AwayFromZero);
                var sale = decimal.Round(cost * (1.25m + number % 5 * 0.04m), 2, MidpointRounding.AwayFromZero);
                return new
                {
                    Code = code,
                    Product = new Product(
                        code,
                        BuildEan13(number),
                        $"{category.Name} demonstração {number:000}",
                        "Produto coerente criado pelo seed local para paginação, busca e filtros.",
                        category.Id,
                        cost,
                        sale)
                };
            })
            .Where(item => !existingCodes.Contains(item.Code, StringComparer.Ordinal))
            .Select(item => item.Product);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCustomersAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Customers
            .Select(customer => customer.Code)
            .ToListAsync(cancellationToken);
        var customers = Enumerable.Range(1, 30)
            .Select(number => new
            {
                Code = $"CLI-{number:0000}",
                Customer = new Customer(
                    $"CLI-{number:0000}",
                    $"Cliente Demonstração {number:000}",
                    $"1{number:0000000000}",
                    $"cliente{number:000}@example.local",
                    $"(11) 90000-{number:0000}")
            })
            .Where(item => !existingCodes.Contains(item.Code, StringComparer.Ordinal))
            .Select(item => item.Customer);
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSuppliersAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Suppliers
            .Select(supplier => supplier.Code)
            .ToListAsync(cancellationToken);
        var suppliers = Enumerable.Range(1, 12)
            .Select(number => new
            {
                Code = $"FOR-{number:0000}",
                Supplier = new Supplier(
                    $"FOR-{number:0000}",
                    $"Fornecedor Demonstração {number:000}",
                    $"9{number:0000000000000}",
                    $"fornecedor{number:000}@example.local",
                    $"(11) 4000-{number:0000}")
            })
            .Where(item => !existingCodes.Contains(item.Code, StringComparer.Ordinal))
            .Select(item => item.Supplier);
        dbContext.Suppliers.AddRange(suppliers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildEan13(int number)
    {
        var firstTwelveDigits = $"789100{number:000000}";
        var weightedSum = firstTwelveDigits
            .Select((character, index) => (character - '0') * (index % 2 == 0 ? 1 : 3))
            .Sum();
        var checkDigit = (10 - weightedSum % 10) % 10;
        return firstTwelveDigits + checkDigit;
    }
}
