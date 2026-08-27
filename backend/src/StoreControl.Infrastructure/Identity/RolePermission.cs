namespace StoreControl.Infrastructure.Identity;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public int PermissionId { get; set; }

    public ApplicationRole Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
