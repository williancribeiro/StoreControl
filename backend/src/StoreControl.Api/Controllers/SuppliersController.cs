using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Common;
using StoreControl.Application.Parties;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.SuppliersRead)]
    public async Task<ActionResult<PagedResult<SupplierSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await supplierService.ListAsync(
            new PartyQuery(page, pageSize, search, isActive, sortBy, sortDirection),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    [ProducesResponseType<SupplierSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierSummary>> Create(
        SupplierCreateRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await supplierService.CreateAsync(Map(request), cancellationToken);
        return Created($"/api/suppliers/{supplier.Id}", supplier);
    }

    [HttpPut("{supplierId:int}")]
    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    public async Task<ActionResult<SupplierSummary>> Update(
        int supplierId,
        SupplierUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await supplierService.UpdateAsync(
            supplierId,
            new UpdateSupplierCommand(
                request.Code,
                request.Name,
                request.Document,
                request.Email,
                request.Phone,
                request.IsActive,
                request.RowVersion),
            cancellationToken));

    private static CreateSupplierCommand Map(SupplierCreateRequest request) =>
        new(request.Code, request.Name, request.Document, request.Email, request.Phone);
}

public record SupplierCreateRequest(
    [Required, MaxLength(30)] string Code,
    [Required, MaxLength(160)] string Name,
    [MaxLength(30)] string? Document,
    [EmailAddress, MaxLength(254)] string? Email,
    [MaxLength(30)] string? Phone);

public sealed record SupplierUpdateRequest(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    [Required] string RowVersion)
    : SupplierCreateRequest(Code, Name, Document, Email, Phone);
