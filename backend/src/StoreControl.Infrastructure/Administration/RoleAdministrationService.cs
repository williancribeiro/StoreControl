using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Administration;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Identity;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Administration;

internal sealed class RoleAdministrationService(StoreControlDbContext dbContext)
    : IRoleAdministrationService
{
    public async Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
        var permissions = await dbContext.RolePermissions
            .AsNoTracking()
            .Select(rolePermission => new { rolePermission.RoleId, rolePermission.Permission.Code })
            .ToListAsync(cancellationToken);

        return roles.Select(role => new RoleSummary(
                role.Id,
                role.Name!,
                role.IsSystemRole,
                permissions
                    .Where(permission => permission.RoleId == role.Id)
                    .Select(permission => permission.Code)
                    .OrderBy(code => code)
                    .ToArray()))
            .ToArray();
    }

    public async Task<RoleSummary> UpdatePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(candidate => candidate.Id == roleId, cancellationToken)
            ?? throw new ResourceNotFoundException("Perfil não encontrado.");

        if (role.Name == RoleNames.Administrator)
        {
            throw new OperationForbiddenException(
                "As permissões do perfil Administrador são fixas para impedir bloqueio administrativo.");
        }

        var requestedCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var permissions = await dbContext.Permissions
            .Where(permission => requestedCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);

        var unknownCodes = requestedCodes.Except(permissions.Select(permission => permission.Code)).ToArray();
        if (unknownCodes.Length > 0)
        {
            throw new RequestValidationException(
                "permissions",
                $"Permissões desconhecidas: {string.Join(", ", unknownCodes)}.");
        }

        var current = await dbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(current);
        dbContext.RolePermissions.AddRange(permissions.Select(permission => new RolePermission
        {
            RoleId = roleId,
            PermissionId = permission.Id
        }));

        var affectedUserIds = await dbContext.UserStoreRoles
            .Where(assignment => assignment.RoleId == roleId)
            .Select(assignment => assignment.UserId)
            .Union(dbContext.Set<IdentityUserRole<Guid>>()
                .Where(userRole => userRole.RoleId == roleId)
                .Select(userRole => userRole.UserId))
            .Distinct()
            .ToListAsync(cancellationToken);
        var affectedUsers = await dbContext.Users
            .Where(user => affectedUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);
        foreach (var user in affectedUsers)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoleSummary(
            role.Id,
            role.Name!,
            role.IsSystemRole,
            permissions.Select(permission => permission.Code).OrderBy(code => code).ToArray());
    }

    public async Task<IReadOnlyCollection<PermissionSummary>> ListPermissionsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Code)
            .Select(permission => new PermissionSummary(permission.Code, permission.Description))
            .ToListAsync(cancellationToken);
}
