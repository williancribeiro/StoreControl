using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Administration;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController(IRoleAdministrationService roleService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.RolesRead)]
    public async Task<ActionResult<IReadOnlyCollection<RoleSummary>>> List(
        CancellationToken cancellationToken) =>
        Ok(await roleService.ListAsync(cancellationToken));

    [HttpGet("permissions")]
    [Authorize(Policy = PermissionCodes.RolesRead)]
    public async Task<ActionResult<IReadOnlyCollection<PermissionSummary>>> ListPermissions(
        CancellationToken cancellationToken) =>
        Ok(await roleService.ListPermissionsAsync(cancellationToken));

    [HttpPut("{roleId:guid}/permissions")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<RoleSummary>> UpdatePermissions(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await roleService.UpdatePermissionsAsync(roleId, request.Permissions, cancellationToken));
}

public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Permissions);
