using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoreControl.Application.Catalog;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Catalog;
using StoreControl.Infrastructure.Persistence;
using StoreControl.Infrastructure.Caching;

namespace StoreControl.Infrastructure.Catalog;

internal sealed class CategoryService(
    StoreControlDbContext dbContext,
    RedisReadCache cache,
    IOptions<CacheOptions> cacheOptions) : ICategoryService
{
    private static readonly string[] AllowedSorts = ["code", "name", "createdAt"];

    public Task<PagedResult<CategorySummary>> ListAsync(
        CategoryQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            return LoadListAsync(query, descending, cancellationToken);
        }

        var variant = FormattableString.Invariant(
            $"{query.Page}|{query.PageSize}|{query.IsActive}|{query.SortBy.ToLowerInvariant()}|{descending}");
        return cache.GetOrCreateAsync(
            "categories", variant, cacheOptions.Value.CategoriesTtlSeconds,
            token => LoadListAsync(query, descending, token), cancellationToken);
    }

    private async Task<PagedResult<CategorySummary>> LoadListAsync(
        CategoryQuery query, bool descending, CancellationToken cancellationToken)
    {
        var categories = dbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(category =>
                category.Code.Contains(search) ||
                category.Name.Contains(search) ||
                (category.Description != null && category.Description.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            categories = categories.Where(category => category.IsActive == query.IsActive.Value);
        }

        var totalCount = await categories.CountAsync(cancellationToken);
        categories = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => categories.OrderBy(category => category.Code),
            ("code", true) => categories.OrderByDescending(category => category.Code),
            ("createdat", false) => categories.OrderBy(category => category.CreatedAtUtc),
            ("createdat", true) => categories.OrderByDescending(category => category.CreatedAtUtc),
            ("name", true) => categories.OrderByDescending(category => category.Name),
            _ => categories.OrderBy(category => category.Name)
        };
        var items = await categories
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CategorySummary>(
            items.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<CategorySummary> CreateAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = CreateEntity(command.Code, command.Name, command.Description);
        if (await dbContext.Categories.AnyAsync(item => item.Code == category.Code, cancellationToken))
        {
            throw Duplicate(category.Code);
        }

        dbContext.Categories.Add(category);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(category.Code);
        }

        return Map(category);
    }

    public async Task<CategorySummary> UpdateAsync(
        int categoryId,
        UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new ResourceNotFoundException("Categoria não encontrada.");
        var expectedRowVersion = MasterDataPersistence.ParseRowVersion(command.RowVersion);

        try
        {
            category.Update(command.Code, command.Name, command.Description, command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }

        dbContext.Entry(category).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "A categoria foi alterada por outro usuário. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(category.Code);
        }

        return Map(category);
    }

    private static Category CreateEntity(string code, string name, string? description)
    {
        try
        {
            return new Category(code, name, description);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
    }

    private static CategorySummary Map(Category category) =>
        new(
            category.Id,
            category.Code,
            category.Name,
            category.Description,
            category.IsActive,
            Convert.ToBase64String(category.RowVersion));

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "category", exception.Message);

    private static ResourceConflictException Duplicate(string code) =>
        new($"Já existe uma categoria com o código {code}.");
}
