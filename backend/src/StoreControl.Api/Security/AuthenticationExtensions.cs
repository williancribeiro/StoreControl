using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StoreControl.Application.Security;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Security;

public static class AuthenticationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddStoreControlSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Authentication:Jwt was not configured.");
        Validate(jwtOptions);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.Issuer) &&
                    !string.IsNullOrWhiteSpace(options.Audience) &&
                    options.SigningKey.Length >= 32 &&
                    options.AccessTokenMinutes is >= 5 and <= 60,
                "JWT configuration is invalid.")
            .ValidateOnStart();
        services.Configure<RefreshTokenCookieOptions>(
            configuration.GetSection(RefreshTokenCookieOptions.SectionName));
        services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync,
                    OnChallenge = context => WriteAuthenticationProblemAsync(
                        context,
                        StatusCodes.Status401Unauthorized,
                        "Autenticação necessária.",
                        "authentication_required"),
                    OnForbidden = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Acesso negado.",
                        "permission_denied")
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionCodes.All)
            {
                options.AddPolicy(permission, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
            }
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var securityStamp = context.Principal?.FindFirst(StoreControlClaimTypes.SecurityStamp)?.Value;
        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
        {
            context.Fail("Invalid token session claims.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<ITokenSessionValidator>();
        if (!await validator.IsValidAsync(userId, securityStamp, context.HttpContext.RequestAborted))
        {
            context.Fail("The user session is no longer valid.");
        }
    }

    private static Task WriteAuthenticationProblemAsync(
        JwtBearerChallengeContext context,
        int status,
        string title,
        string errorCode)
    {
        context.HandleResponse();
        return WriteProblemAsync(context.HttpContext, status, title, errorCode);
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string errorCode)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            options.SigningKey.Length < 32 ||
            options.AccessTokenMinutes is < 5 or > 60)
        {
            throw new InvalidOperationException(
                "Configure Authentication:Jwt with issuer, audience, a signing key of at least 32 characters, " +
                "and an access-token lifetime between 5 and 60 minutes.");
        }
    }
}
