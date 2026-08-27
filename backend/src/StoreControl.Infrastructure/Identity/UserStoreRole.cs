using StoreControl.Domain.Stores;

namespace StoreControl.Infrastructure.Identity;

public sealed class UserStoreRole
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public int StoreId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public ApplicationRole Role { get; set; } = null!;

    public Store Store { get; set; } = null!;
}
