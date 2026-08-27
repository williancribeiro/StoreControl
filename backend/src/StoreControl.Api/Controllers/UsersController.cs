using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Administration;
using StoreControl.Application.Common;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(IUserAdministrationService userService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.UsersRead)]
    public async Task<ActionResult<PagedResult<UserSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await userService.ListAsync(
            User.GetRequiredUserId(),
            page,
            pageSize,
            search,
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    [ProducesResponseType<UserSummary>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserSummary>> Create(
        UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(
            new CreateUserCommand(
                request.FullName,
                request.Email,
                request.Password,
                request.IsAdministrator,
                request.StoreAssignments.Select(Map).ToArray()),
            cancellationToken);
        return Created($"/api/users/{user.Id}", user);
    }

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<ActionResult<UserSummary>> Update(
        Guid userId,
        UserUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userService.UpdateAsync(
            User.GetRequiredUserId(),
            userId,
            new UpdateUserCommand(
                request.FullName,
                request.IsActive,
                request.IsAdministrator,
                request.StoreAssignments.Select(Map).ToArray()),
            cancellationToken));

    [HttpPost("{userId:guid}/reset-password")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userService.ResetPasswordAsync(
            User.GetRequiredUserId(),
            userId,
            request.NewPassword,
            cancellationToken);
        return NoContent();
    }

    private static UserRoleAssignmentInput Map(UserStoreAssignmentRequest request) =>
        new(request.StoreId, request.RoleId);
}

public sealed record UserStoreAssignmentRequest(int StoreId, Guid RoleId);

public sealed record UserCreateRequest(
    [Required, MaxLength(160)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    bool IsAdministrator,
    IReadOnlyCollection<UserStoreAssignmentRequest> StoreAssignments);

public sealed record UserUpdateRequest(
    [Required, MaxLength(160)] string FullName,
    bool IsActive,
    bool IsAdministrator,
    IReadOnlyCollection<UserStoreAssignmentRequest> StoreAssignments);

public sealed record ResetPasswordRequest(
    [Required, MinLength(12), MaxLength(128)] string NewPassword);
