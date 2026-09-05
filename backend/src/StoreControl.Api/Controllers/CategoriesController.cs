using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Catalog;
using StoreControl.Application.Common;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.CategoriesRead)]
    public async Task<ActionResult<PagedResult<CategorySummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await categoryService.ListAsync(
            new CategoryQuery(page, pageSize, search, isActive, sortBy, sortDirection),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [ProducesResponseType<CategorySummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CategorySummary>> Create(
        CategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(
            new CreateCategoryCommand(request.Code, request.Name, request.Description),
            cancellationToken);
        return Created($"/api/categories/{category.Id}", category);
    }

    [HttpPut("{categoryId:int}")]
    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    public async Task<ActionResult<CategorySummary>> Update(
        int categoryId,
        CategoryUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await categoryService.UpdateAsync(
            categoryId,
            new UpdateCategoryCommand(
                request.Code,
                request.Name,
                request.Description,
                request.IsActive,
                request.RowVersion),
            cancellationToken));
}

public sealed record CategoryCreateRequest(
    [Required, MaxLength(30)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(500)] string? Description);

public sealed record CategoryUpdateRequest(
    [Required, MaxLength(30)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(500)] string? Description,
    bool IsActive,
    [Required] string RowVersion);
