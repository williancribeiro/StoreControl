using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Common;
using StoreControl.Application.Parties;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.CustomersRead)]
    public async Task<ActionResult<PagedResult<CustomerSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await customerService.ListAsync(
            new PartyQuery(page, pageSize, search, isActive, sortBy, sortDirection),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.CustomersManage)]
    [ProducesResponseType<CustomerSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerSummary>> Create(
        CustomerCreateRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.CreateAsync(Map(request), cancellationToken);
        return Created($"/api/customers/{customer.Id}", customer);
    }

    [HttpPut("{customerId:int}")]
    [Authorize(Policy = PermissionCodes.CustomersManage)]
    public async Task<ActionResult<CustomerSummary>> Update(
        int customerId,
        CustomerUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await customerService.UpdateAsync(
            customerId,
            new UpdateCustomerCommand(
                request.Code,
                request.Name,
                request.Document,
                request.Email,
                request.Phone,
                request.IsActive,
                request.RowVersion),
            cancellationToken));

    private static CreateCustomerCommand Map(CustomerCreateRequest request) =>
        new(request.Code, request.Name, request.Document, request.Email, request.Phone);
}

public record CustomerCreateRequest(
    [Required, MaxLength(30)] string Code,
    [Required, MaxLength(160)] string Name,
    [MaxLength(30)] string? Document,
    [EmailAddress, MaxLength(254)] string? Email,
    [MaxLength(30)] string? Phone);

public sealed record CustomerUpdateRequest(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    [Required] string RowVersion)
    : CustomerCreateRequest(Code, Name, Document, Email, Phone);
