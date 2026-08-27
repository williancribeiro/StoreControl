using StoreControl.Application.Common;

namespace StoreControl.Application.Catalog;

public sealed record CategorySummary(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    string RowVersion);

public sealed record CategoryQuery(
    int Page,
    int PageSize,
    string? Search,
    bool? IsActive,
    string SortBy,
    string SortDirection);

public sealed record CreateCategoryCommand(string Code, string Name, string? Description);

public sealed record UpdateCategoryCommand(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    string RowVersion);

public interface ICategoryService
{
    Task<PagedResult<CategorySummary>> ListAsync(CategoryQuery query, CancellationToken cancellationToken);

    Task<CategorySummary> CreateAsync(CreateCategoryCommand command, CancellationToken cancellationToken);

    Task<CategorySummary> UpdateAsync(
        int categoryId,
        UpdateCategoryCommand command,
        CancellationToken cancellationToken);
}

public sealed record ProductSummary(
    int Id,
    string Code,
    string? Barcode,
    string Name,
    string? Description,
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    decimal CostPrice,
    decimal SalePrice,
    bool IsActive,
    string RowVersion);

public sealed record ProductQuery(
    int Page,
    int PageSize,
    string? Search,
    int? CategoryId,
    bool? IsActive,
    string SortBy,
    string SortDirection);

public sealed record CreateProductCommand(
    string Code,
    string? Barcode,
    string Name,
    string? Description,
    int CategoryId,
    decimal CostPrice,
    decimal SalePrice);

public sealed record UpdateProductCommand(
    string Code,
    string? Barcode,
    string Name,
    string? Description,
    int CategoryId,
    decimal CostPrice,
    decimal SalePrice,
    bool IsActive,
    string RowVersion);

public interface IProductService
{
    Task<PagedResult<ProductSummary>> ListAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductSummary> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken);

    Task<ProductSummary> UpdateAsync(
        int productId,
        UpdateProductCommand command,
        CancellationToken cancellationToken);
}
