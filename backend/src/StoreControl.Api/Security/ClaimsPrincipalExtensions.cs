using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace StoreControl.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new InvalidOperationException("O token autenticado não possui um subject válido.");
    }
}
