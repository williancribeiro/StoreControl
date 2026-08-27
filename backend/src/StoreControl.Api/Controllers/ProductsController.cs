using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Catalog;
using StoreControl.Application.Common;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ProductsRead)]
    public async Task<ActionResult<PagedResult<ProductSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await productService.ListAsync(
            new ProductQuery(page, pageSize, search, categoryId, isActive, sortBy, sortDirection),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [ProducesResponseType<ProductSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductSummary>> Create(
        ProductCreateRequest request,
        CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(Map(request), cancellationToken);
        return Created($"/api/products/{product.Id}", product);
    }

    [HttpPut("{productId:int}")]
    [Authorize(Policy = PermissionCodes.ProductsManage)]
    public async Task<ActionResult<ProductSummary>> Update(
        int productId,
        ProductUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productService.UpdateAsync(
            productId,
            new UpdateProductCommand(
                request.Code,
                request.Barcode,
                request.Name,
                request.Description,
                request.CategoryId,
                request.CostPrice,
                request.SalePrice,
                request.IsActive,
                request.RowVersion),
            cancellationToken));

    private static CreateProductCommand Map(ProductCreateRequest request) =>
        new(
            request.Code,
            request.Barcode,
            request.Name,
            request.Description,
            request.CategoryId,
            request.CostPrice,
            request.SalePrice);
}

public record ProductCreateRequest(
    [Required, MaxLength(30)] string Code,
    [RegularExpression("^(?:[0-9]{8}|[0-9]{12}|[0-9]{13}|[0-9]{14})$")] string? Barcode,
    [Required, MaxLength(160)] string Name,
    [MaxLength(1000)] string? Description,
    [Range(1, int.MaxValue)] int CategoryId,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal CostPrice,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal SalePrice);

public sealed record ProductUpdateRequest(
    string Code,
    string? Barcode,
    string Name,
    string? Description,
    int CategoryId,
    decimal CostPrice,
    decimal SalePrice,
    bool IsActive,
    [Required] string RowVersion)
    : ProductCreateRequest(Code, Barcode, Name, Description, CategoryId, CostPrice, SalePrice);
