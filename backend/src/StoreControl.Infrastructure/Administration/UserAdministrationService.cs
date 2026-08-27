using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Administration;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Identity;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Administration;

internal sealed class UserAdministrationService(
    UserManager<ApplicationUser> userManager,
    StoreControlDbContext dbContext) : IUserAdministrationService
{
    public async Task<PagedResult<UserSummary>> ListAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new RequestValidationException("pagination", "Use page >= 1 e pageSize entre 1 e 100.");
        }

        var isAdministrator = await IsAdministratorAsync(currentUserId, cancellationToken);
        var query = dbContext.Users.AsNoTracking();

        if (!isAdministrator)
        {
            var accessibleStoreIds = dbContext.UserStoreRoles
                .Where(assignment => assignment.UserId == currentUserId)
                .Select(assignment => assignment.StoreId);
            query = query.Where(user => dbContext.UserStoreRoles.Any(
                assignment => assignment.UserId == user.Id && accessibleStoreIds.Contains(assignment.StoreId)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user =>
                user.FullName.Contains(term) ||
                (user.Email != null && user.Email.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = await MapUsersAsync(users, cancellationToken);

        return new PagedResult<UserSummary>(items, page, pageSize, totalCount);
    }

    public async Task<UserSummary> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var fullName = ValidateFullName(command.FullName);
        var assignments = await ValidateAssignmentsAsync(command.StoreAssignments, cancellationToken);
        ValidateRequiredAccess(command.IsAdministrator, true, assignments);
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var userId = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = command.Email.Trim(),
                Email = command.Email.Trim(),
                FullName = fullName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };

            EnsureSucceeded(await userManager.CreateAsync(user, command.Password), "password");
            if (command.IsAdministrator)
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, RoleNames.Administrator), "role");
            }

            dbContext.UserStoreRoles.AddRange(assignments.Select(assignment => new UserStoreRole
            {
                UserId = user.Id,
                StoreId = assignment.StoreId,
                RoleId = assignment.RoleId
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user.Id;
        });

        var created = await dbContext.Users.AsNoTracking()
            .SingleAsync(user => user.Id == userId, cancellationToken);
        return (await MapUsersAsync([created], cancellationToken)).Single();
    }

    public async Task<UserSummary> UpdateAsync(
        Guid currentUserId,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var fullName = ValidateFullName(command.FullName);
        var assignments = await ValidateAssignmentsAsync(command.StoreAssignments, cancellationToken);
        ValidateRequiredAccess(command.IsAdministrator, command.IsActive, assignments);
        var currentIsAdministrator = await IsAdministratorAsync(userId, cancellationToken);

        if (userId == currentUserId && (!command.IsActive || currentIsAdministrator && !command.IsAdministrator))
        {
            throw new OperationForbiddenException(
                "O usuário atual não pode desativar a própria conta nem remover o próprio perfil Administrador.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = await userManager.FindByIdAsync(userId.ToString())
                ?? throw new ResourceNotFoundException("Usuário não encontrado.");

            user.FullName = fullName;
            user.IsActive = command.IsActive;
            EnsureSucceeded(await userManager.UpdateAsync(user), "user");

            var hasAdministratorRole = await userManager.IsInRoleAsync(user, RoleNames.Administrator);
            if (command.IsAdministrator && !hasAdministratorRole)
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, RoleNames.Administrator), "role");
            }
            else if (!command.IsAdministrator && hasAdministratorRole)
            {
                EnsureSucceeded(await userManager.RemoveFromRoleAsync(user, RoleNames.Administrator), "role");
            }

            var existingAssignments = await dbContext.UserStoreRoles
                .Where(assignment => assignment.UserId == userId)
                .ToListAsync(cancellationToken);
            dbContext.UserStoreRoles.RemoveRange(existingAssignments);
            dbContext.UserStoreRoles.AddRange(assignments.Select(assignment => new UserStoreRole
            {
                UserId = userId,
                StoreId = assignment.StoreId,
                RoleId = assignment.RoleId
            }));

            user.SecurityStamp = Guid.NewGuid().ToString("N");
            EnsureSucceeded(await userManager.UpdateAsync(user), "user");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        var updated = await dbContext.Users.AsNoTracking()
            .SingleAsync(user => user.Id == userId, cancellationToken);
        return (await MapUsersAsync([updated], cancellationToken)).Single();
    }

    public async Task ResetPasswordAsync(
        Guid currentUserId,
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new ResourceNotFoundException("Usuário não encontrado.");
        var passwordErrors = new List<IdentityError>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, newPassword);
            if (!result.Succeeded)
            {
                passwordErrors.AddRange(result.Errors);
            }
        }

        if (passwordErrors.Count > 0)
        {
            EnsureSucceeded(IdentityResult.Failed(passwordErrors.ToArray()), "password");
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        EnsureSucceeded(await userManager.UpdateAsync(user), "password");

        // ResetPassword updates the security stamp; refresh sessions are also revoked explicitly.
        await RevokeActiveRefreshTokensAsync(userId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<UserRoleAssignmentInput>> ValidateAssignmentsAsync(
        IReadOnlyCollection<UserRoleAssignmentInput> assignments,
        CancellationToken cancellationToken)
    {
        var materialized = assignments.Distinct().ToArray();
        if (materialized.Select(assignment => assignment.StoreId).Distinct().Count() != materialized.Length)
        {
            throw new RequestValidationException("storeAssignments", "Informe no máximo um perfil por loja.");
        }

        var storeIds = materialized.Select(assignment => assignment.StoreId).ToArray();
        var activeStoreIds = await dbContext.Stores
            .Where(store => storeIds.Contains(store.Id) && store.IsActive)
            .Select(store => store.Id)
            .ToListAsync(cancellationToken);
        var invalidStores = storeIds.Except(activeStoreIds).ToArray();
        if (invalidStores.Length > 0)
        {
            throw new RequestValidationException(
                "storeAssignments",
                $"Lojas inexistentes ou inativas: {string.Join(", ", invalidStores)}.");
        }

        var roleIds = materialized.Select(assignment => assignment.RoleId).ToArray();
        var validRoleIds = await dbContext.Roles
            .Where(role => roleIds.Contains(role.Id) && role.Name != RoleNames.Administrator)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        var invalidRoles = roleIds.Except(validRoleIds).ToArray();
        if (invalidRoles.Length > 0)
        {
            throw new RequestValidationException(
                "storeAssignments",
                "Perfis de loja inválidos. Administrador é um perfil global.");
        }

        return materialized;
    }

    private async Task<IReadOnlyCollection<UserSummary>> MapUsersAsync(
        IReadOnlyCollection<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var userIds = users.Select(user => user.Id).ToArray();
        var administratorRoleId = await dbContext.Roles
            .Where(role => role.Name == RoleNames.Administrator)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var administratorIds = administratorRoleId is null
            ? []
            : await dbContext.Set<IdentityUserRole<Guid>>()
                .Where(userRole => userRole.RoleId == administratorRoleId && userIds.Contains(userRole.UserId))
                .Select(userRole => userRole.UserId)
                .ToListAsync(cancellationToken);
        var assignments = await dbContext.UserStoreRoles
            .AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.UserId))
            .OrderBy(assignment => assignment.Store.Name)
            .Select(assignment => new
            {
                assignment.UserId,
                Item = new UserStoreAssignment(
                    assignment.StoreId,
                    assignment.Store.Code,
                    assignment.Store.Name,
                    assignment.RoleId,
                    assignment.Role.Name!)
            })
            .ToListAsync(cancellationToken);

        return users.Select(user => new UserSummary(
                user.Id,
                user.FullName,
                user.Email!,
                user.IsActive,
                administratorIds.Contains(user.Id),
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                assignments
                    .Where(assignment => assignment.UserId == user.Id)
                    .Select(assignment => assignment.Item)
                    .ToArray()))
            .ToArray();
    }

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == RoleNames.Administrator
         select userRole).AnyAsync(cancellationToken);

    private async Task RevokeActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateFullName(string fullName)
    {
        var normalized = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new RequestValidationException("fullName", "O nome deve possuir entre 1 e 160 caracteres.");
        }

        return normalized;
    }

    private static void ValidateRequiredAccess(
        bool isAdministrator,
        bool isActive,
        IReadOnlyCollection<UserRoleAssignmentInput> assignments)
    {
        if (isActive && !isAdministrator && assignments.Count == 0)
        {
            throw new RequestValidationException(
                "storeAssignments",
                "Um usuário ativo não administrador deve possuir ao menos um perfil de loja.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string field)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = result.Errors.Select(error => error.Description).ToArray()
        });
    }
}
