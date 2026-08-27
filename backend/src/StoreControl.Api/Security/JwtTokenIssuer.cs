using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StoreControl.Application.Security;

namespace StoreControl.Api.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken Issue(AccessTokenDescriptor descriptor)
    {
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, descriptor.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, descriptor.Email),
            new(JwtRegisteredClaimNames.Name, descriptor.FullName),
            new(StoreControlClaimTypes.SecurityStamp, descriptor.SecurityStamp)
        };
        claims.AddRange(descriptor.Roles.Select(role => new Claim("role", role)));
        claims.AddRange(descriptor.Permissions.Select(
            permission => new Claim(StoreControlClaimTypes.Permission, permission)));
        claims.AddRange(descriptor.Stores.Select(
            store => new Claim(StoreControlClaimTypes.Store, store.StoreId.ToString())));
        claims.AddRange(descriptor.Stores.Select(
            store => new Claim(StoreControlClaimTypes.StoreRole, $"{store.StoreId}:{store.Role}")));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            issuedAtUtc,
            expiresAtUtc,
            credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
