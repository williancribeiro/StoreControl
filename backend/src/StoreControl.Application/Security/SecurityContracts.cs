namespace StoreControl.Application.Security;

public sealed record ClientSessionContext(string? IpAddress, string? UserAgent);

public sealed record LoginCommand(string Email, string Password, ClientSessionContext Client);

public sealed record AuthenticatedSession(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    CurrentUser User);

public enum AuthenticationFailure
{
    None,
    InvalidCredentials,
    LockedOut,
    InvalidRefreshToken
}

public sealed record AuthenticationResult(AuthenticationFailure Failure, AuthenticatedSession? Session)
{
    public static AuthenticationResult Success(AuthenticatedSession session) =>
        new(AuthenticationFailure.None, session);

    public static AuthenticationResult Failed(AuthenticationFailure failure) =>
        new(failure, null);
}

public sealed record CurrentUser(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserStoreAccess> Stores);

public sealed record UserStoreAccess(int StoreId, string StoreCode, string StoreName, string Role);

public sealed record AccessTokenDescriptor(
    Guid UserId,
    string FullName,
    string Email,
    string SecurityStamp,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserStoreAccess> Stores);

public sealed record IssuedAccessToken(string Value, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenDescriptor descriptor);
}

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        ClientSessionContext client,
        CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, ClientSessionContext client, CancellationToken cancellationToken);

    Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ITokenSessionValidator
{
    Task<bool> IsValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
}
