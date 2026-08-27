using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Catalog;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Catalog;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Catalog;

internal sealed class ProductService(StoreControlDbContext dbContext) : IProductService
{
    private static readonly string[] AllowedSorts = ["code", "name", "category", "costPrice", "salePrice", "createdAt"];

    public async Task<PagedResult<ProductSummary>> ListAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        var products = dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(product =>
                product.Code.Contains(search) ||
                (product.Barcode != null && product.Barcode.Contains(search)) ||
                product.Name.Contains(search) ||
                (product.Description != null && product.Description.Contains(search)));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(product => product.CategoryId == query.CategoryId.Value);
        }

        if (query.IsActive.HasValue)
        {
            products = products.Where(product => product.IsActive == query.IsActive.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);
        products = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => products.OrderBy(product => product.Code),
            ("code", true) => products.OrderByDescending(product => product.Code),
            ("category", false) => products.OrderBy(product => product.Category.Name).ThenBy(product => product.Name),
            ("category", true) => products.OrderByDescending(product => product.Category.Name).ThenBy(product => product.Name),
            ("costprice", false) => products.OrderBy(product => product.CostPrice).ThenBy(product => product.Name),
            ("costprice", true) => products.OrderByDescending(product => product.CostPrice).ThenBy(product => product.Name),
            ("saleprice", false) => products.OrderBy(product => product.SalePrice).ThenBy(product => product.Name),
            ("saleprice", true) => products.OrderByDescending(product => product.SalePrice).ThenBy(product => product.Name),
            ("createdat", false) => products.OrderBy(product => product.CreatedAtUtc),
            ("createdat", true) => products.OrderByDescending(product => product.CreatedAtUtc),
            ("name", true) => products.OrderByDescending(product => product.Name),
            _ => products.OrderBy(product => product.Name)
        };
        var items = await products
            .Include(product => product.Category)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSummary>(
            items.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<ProductSummary> CreateAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var category = await GetCategoryForAssignmentAsync(command.CategoryId, cancellationToken);
        var product = CreateEntity(command);
        await EnsureUniqueAsync(product, null, cancellationToken);
        dbContext.Products.Add(product);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(product, exception);
        }

        return Map(product, category);
    }

    public async Task<ProductSummary> UpdateAsync(
        int productId,
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken)
            ?? throw new ResourceNotFoundException("Produto não encontrado.");
        var category = await GetCategoryForAssignmentAsync(
            command.CategoryId,
            cancellationToken,
            product.CategoryId);
        var expectedRowVersion = MasterDataPersistence.ParseRowVersion(command.RowVersion);

        try
        {
            product.Update(
                command.Code,
                command.Barcode,
                command.Name,
                command.Description,
                command.CategoryId,
                command.CostPrice,
                command.SalePrice,
                command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }

        await EnsureUniqueAsync(product, productId, cancellationToken);
        dbContext.Entry(product).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "O produto foi alterado por outro usuário. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(product, exception);
        }

        return Map(product, category);
    }

    private async Task<Category> GetCategoryForAssignmentAsync(
        int categoryId,
        CancellationToken cancellationToken,
        int? currentCategoryId = null)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new RequestValidationException("categoryId", "A categoria informada não existe.");

        if (!category.IsActive && currentCategoryId != categoryId)
        {
            throw new RequestValidationException("categoryId", "A categoria informada está inativa.");
        }

        return category;
    }

    private async Task EnsureUniqueAsync(Product product, int? currentId, CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(
                item => item.Code == product.Code && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um produto com o código {product.Code}.");
        }

        if (product.Barcode is not null && await dbContext.Products.AnyAsync(
                item => item.Barcode == product.Barcode && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um produto com o código de barras {product.Barcode}.");
        }
    }

    private static Product CreateEntity(CreateProductCommand command)
    {
        try
        {
            return new Product(
                command.Code,
                command.Barcode,
                command.Name,
                command.Description,
                command.CategoryId,
                command.CostPrice,
                command.SalePrice);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
    }

    private static ProductSummary Map(Product product) => Map(product, product.Category);

    private static ProductSummary Map(Product product, Category category) =>
        new(
            product.Id,
            product.Code,
            product.Barcode,
            product.Name,
            product.Description,
            product.CategoryId,
            category.Code,
            category.Name,
            product.CostPrice,
            product.SalePrice,
            product.IsActive,
            Convert.ToBase64String(product.RowVersion));

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "product", exception.Message);

    private static ResourceConflictException Duplicate(Product product, DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Barcode", StringComparison.OrdinalIgnoreCase) == true
            ? new ResourceConflictException($"Já existe um produto com o código de barras {product.Barcode}.")
            : new ResourceConflictException($"Já existe um produto com o código {product.Code}.");
}
