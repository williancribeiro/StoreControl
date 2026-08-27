using Microsoft.AspNetCore.Identity;

namespace StoreControl.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public bool IsSystemRole { get; set; }
}
