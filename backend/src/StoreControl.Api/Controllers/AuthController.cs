using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StoreControl.Api.Security;
using StoreControl.Application.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IOptions<RefreshTokenCookieOptions> cookieOptions) : ControllerBase
{
    private const string RefreshTokenCookieName = ".StoreControl.RefreshToken";

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicyNames.Authentication)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new LoginCommand(request.Email, request.Password, GetClientContext()),
            cancellationToken);
        if (result.Session is null)
        {
            return AuthenticationFailed();
        }

        SetRefreshTokenCookie(result.Session);
        return Ok(Map(result.Session));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicyNames.Authentication)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthenticationFailed();
        }

        var result = await authenticationService.RefreshAsync(
            refreshToken,
            GetClientContext(),
            cancellationToken);
        if (result.Session is null)
        {
            DeleteRefreshTokenCookie();
            return AuthenticationFailed();
        }

        SetRefreshTokenCookie(result.Session);
        return Ok(Map(result.Session));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken);
        await authenticationService.LogoutAsync(refreshToken, GetClientContext(), cancellationToken);
        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUser>> Me(CancellationToken cancellationToken)
    {
        var currentUser = await authenticationService.GetCurrentUserAsync(
            User.GetRequiredUserId(),
            cancellationToken);
        return currentUser is null ? AuthenticationFailed() : Ok(currentUser);
    }

    private ClientSessionContext GetClientContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());

    private void SetRefreshTokenCookie(AuthenticatedSession session)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            session.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieOptions.Value.CookieSecure,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                Expires = session.RefreshTokenExpiresAtUtc,
                IsEssential = true
            });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieOptions.Value.CookieSecure,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                IsEssential = true
            });
    }

    private UnauthorizedObjectResult AuthenticationFailed() => Unauthorized(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Não foi possível autenticar.",
        Detail = "E-mail, senha ou sessão inválidos.",
        Type = "https://httpstatuses.com/401",
        Extensions =
        {
            ["errorCode"] = "invalid_authentication",
            ["traceId"] = HttpContext.TraceIdentifier,
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    });

    private static AuthResponse Map(AuthenticatedSession session) =>
        new(session.AccessToken, session.AccessTokenExpiresAtUtc, session.User);
}

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    CurrentUser User);
