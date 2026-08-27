using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Administration;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/stores")]
[Authorize]
public sealed class StoresController(IStoreAdministrationService storeService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.StoresRead)]
    public async Task<ActionResult<IReadOnlyCollection<StoreSummary>>> List(
        CancellationToken cancellationToken) =>
        Ok(await storeService.ListAccessibleAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.StoresManage)]
    [ProducesResponseType<StoreSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StoreSummary>> Create(
        StoreRequest request,
        CancellationToken cancellationToken)
    {
        var store = await storeService.CreateAsync(
            new CreateStoreCommand(request.Code, request.Name),
            cancellationToken);
        return Created($"/api/stores/{store.Id}", store);
    }

    [HttpPut("{storeId:int}")]
    [Authorize(Policy = PermissionCodes.StoresManage)]
    public async Task<ActionResult<StoreSummary>> Update(
        int storeId,
        UpdateStoreRequest request,
        CancellationToken cancellationToken) =>
        Ok(await storeService.UpdateAsync(
            storeId,
            new UpdateStoreCommand(request.Code, request.Name, request.IsActive, request.RowVersion),
            cancellationToken));
}

public sealed record StoreRequest(
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(120)] string Name);

public sealed record UpdateStoreRequest(
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(120)] string Name,
    bool IsActive,
    [Required] string RowVersion);
