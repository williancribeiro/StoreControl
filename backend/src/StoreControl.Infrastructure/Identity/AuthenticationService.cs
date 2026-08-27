using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoreControl.Application.Security;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Identity;

internal sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    StoreControlDbContext dbContext,
    IAccessTokenIssuer accessTokenIssuer,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private readonly TimeSpan _refreshTokenLifetime =
        TimeSpan.FromDays(refreshTokenOptions.Value.LifetimeDays);

    public async Task<AuthenticationResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || !user.IsActive)
        {
            // Do not persist a user-supplied identifier for a failed login in application logs.
            logger.LogWarning("Login rejected for an unknown or inactive account");
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login rejected for locked user {UserId}", user.Id);
            return AuthenticationResult.Failed(AuthenticationFailure.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            var failure = await userManager.IsLockedOutAsync(user)
                ? AuthenticationFailure.LockedOut
                : AuthenticationFailure.InvalidCredentials;
            logger.LogWarning("Invalid password for user {UserId}", user.Id);
            return AuthenticationResult.Failed(failure);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await EnsureIdentityUpdateSucceededAsync(userManager.UpdateAsync(user));

        var session = await CreateSessionAsync(
            user,
            Guid.NewGuid(),
            command.Client,
            cancellationToken);

        logger.LogInformation("User {UserId} authenticated", user.Id);
        return AuthenticationResult.Success(session);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        ClientSessionContext client,
        CancellationToken cancellationToken)
    {
        var hash = RefreshTokenProtector.Hash(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (storedToken is null)
        {
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        var utcNow = DateTime.UtcNow;
        if (storedToken.RevokedAtUtc is not null)
        {
            var activeFamilyTokens = await dbContext.RefreshTokens
                .Where(token => token.FamilyId == storedToken.FamilyId && token.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var familyToken in activeFamilyTokens)
            {
                familyToken.RevokedAtUtc = utcNow;
                familyToken.RevokedByIp = Normalize(client.IpAddress, 64);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Refresh token replay detected for user {UserId} and family {FamilyId}",
                storedToken.UserId,
                storedToken.FamilyId);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        if (storedToken.IsExpired(utcNow) || !storedToken.User.IsActive)
        {
            storedToken.RevokedAtUtc = utcNow;
            storedToken.RevokedByIp = Normalize(client.IpAddress, 64);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        var (plainTextToken, replacement) = CreateRefreshToken(
            storedToken.UserId,
            storedToken.FamilyId,
            client,
            utcNow);

        storedToken.RevokedAtUtc = utcNow;
        storedToken.RevokedByIp = Normalize(client.IpAddress, 64);
        storedToken.ReplacedByTokenId = replacement.Id;
        dbContext.RefreshTokens.Add(replacement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Concurrent refresh rejected for token {RefreshTokenId}", storedToken.Id);
            return AuthenticationResult.Failed(AuthenticationFailure.InvalidRefreshToken);
        }

        var session = await BuildSessionAsync(
            storedToken.User,
            plainTextToken,
            replacement.ExpiresAtUtc,
            cancellationToken);

        return AuthenticationResult.Success(session);
    }

    public async Task LogoutAsync(
        string? refreshToken,
        ClientSessionContext client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = RefreshTokenProtector.Hash(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc is not null)
        {
            return;
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedByIp = Normalize(client.IpAddress, 64);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refresh session revoked for user {UserId}", storedToken.UserId);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);

        return user is null ? null : await BuildCurrentUserAsync(user, cancellationToken);
    }

    private async Task<AuthenticatedSession> CreateSessionAsync(
        ApplicationUser user,
        Guid familyId,
        ClientSessionContext client,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var (plainTextToken, refreshToken) = CreateRefreshToken(user.Id, familyId, client, utcNow);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildSessionAsync(user, plainTextToken, refreshToken.ExpiresAtUtc, cancellationToken);
    }

    private async Task<AuthenticatedSession> BuildSessionAsync(
        ApplicationUser user,
        string refreshToken,
        DateTime refreshExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var currentUser = await BuildCurrentUserAsync(user, cancellationToken);
        var descriptor = new AccessTokenDescriptor(
            user.Id,
            user.FullName,
            user.Email!,
            user.SecurityStamp!,
            currentUser.Roles,
            currentUser.Permissions,
            currentUser.Stores);
        var accessToken = accessTokenIssuer.Issue(descriptor);

        return new AuthenticatedSession(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken,
            refreshExpiresAtUtc,
            currentUser);
    }

    private async Task<CurrentUser> BuildCurrentUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var globalRoles = await userManager.GetRolesAsync(user);
        var storeAssignments = await dbContext.UserStoreRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == user.Id && assignment.Store.IsActive)
            .OrderBy(assignment => assignment.Store.Name)
            .Select(assignment => new UserStoreAccess(
                assignment.StoreId,
                assignment.Store.Code,
                assignment.Store.Name,
                assignment.Role.Name!))
            .ToListAsync(cancellationToken);

        var roleNames = globalRoles
            .Concat(storeAssignments.Select(assignment => assignment.Role))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToArray();

        var roleIds = await dbContext.Roles
            .Where(role => role.Name != null && roleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        var permissions = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .Select(rolePermission => rolePermission.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return new CurrentUser(
            user.Id,
            user.FullName,
            user.Email!,
            roleNames,
            permissions,
            storeAssignments);
    }

    private (string PlainText, RefreshToken Entity) CreateRefreshToken(
        Guid userId,
        Guid familyId,
        ClientSessionContext client,
        DateTime utcNow)
    {
        var plainText = RefreshTokenProtector.Generate();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = RefreshTokenProtector.Hash(plainText),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(_refreshTokenLifetime),
            CreatedByIp = Normalize(client.IpAddress, 64),
            UserAgent = Normalize(client.UserAgent, 300)
        };

        return (plainText, entity);
    }

    private static string? Normalize(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];

    private static async Task EnsureIdentityUpdateSucceededAsync(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity update failed: {string.Join(", ", result.Errors.Select(error => error.Code))}");
        }
    }
}
