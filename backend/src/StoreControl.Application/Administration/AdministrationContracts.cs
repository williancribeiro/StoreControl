using StoreControl.Application.Common;

namespace StoreControl.Application.Administration;

public sealed record StoreSummary(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    string RowVersion);

public sealed record CreateStoreCommand(string Code, string Name);

public sealed record UpdateStoreCommand(string Code, string Name, bool IsActive, string RowVersion);

public interface IStoreAdministrationService
{
    Task<IReadOnlyCollection<StoreSummary>> ListAccessibleAsync(Guid userId, CancellationToken cancellationToken);

    Task<StoreSummary> CreateAsync(CreateStoreCommand command, CancellationToken cancellationToken);

    Task<StoreSummary> UpdateAsync(int storeId, UpdateStoreCommand command, CancellationToken cancellationToken);
}

public sealed record PermissionSummary(string Code, string Description);

public sealed record RoleSummary(
    Guid Id,
    string Name,
    bool IsSystemRole,
    IReadOnlyCollection<string> Permissions);

public interface IRoleAdministrationService
{
    Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken cancellationToken);

    Task<RoleSummary> UpdatePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken);
}

public sealed record UserRoleAssignmentInput(int StoreId, Guid RoleId);

public sealed record UserStoreAssignment(
    int StoreId,
    string StoreCode,
    string StoreName,
    Guid RoleId,
    string RoleName);

public sealed record UserSummary(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    bool IsAdministrator,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<UserStoreAssignment> StoreAssignments);

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string Password,
    bool IsAdministrator,
    IReadOnlyCollection<UserRoleAssignmentInput> StoreAssignments);

public sealed record UpdateUserCommand(
    string FullName,
    bool IsActive,
    bool IsAdministrator,
    IReadOnlyCollection<UserRoleAssignmentInput> StoreAssignments);

public interface IUserAdministrationService
{
    Task<PagedResult<UserSummary>> ListAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<UserSummary> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken);

    Task<UserSummary> UpdateAsync(
        Guid currentUserId,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken);

    Task ResetPasswordAsync(
        Guid currentUserId,
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken);
}
