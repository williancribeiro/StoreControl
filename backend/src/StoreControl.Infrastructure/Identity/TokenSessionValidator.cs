using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Security;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Identity;

internal sealed class TokenSessionValidator(StoreControlDbContext dbContext) : ITokenSessionValidator
{
    public Task<bool> IsValidAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId &&
                    user.IsActive &&
                    user.SecurityStamp == securityStamp,
                cancellationToken);
}
