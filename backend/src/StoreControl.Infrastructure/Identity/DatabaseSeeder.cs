using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoreControl.Application.Abstractions.Persistence;
using StoreControl.Domain.Security;
using StoreControl.Domain.Stores;
using StoreControl.Infrastructure.Persistence;
using StoreControl.Infrastructure.Persistence.Models;
using StoreControl.Infrastructure.Persistence.Seeding;

namespace StoreControl.Infrastructure.Identity;

internal sealed class DatabaseSeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    StoreControlDbContext dbContext,
    MasterDataSeeder masterDataSeeder,
    InventorySeeder inventorySeeder,
    IOptions<DemoSeedOptions> seedOptions,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    private static readonly IReadOnlyDictionary<string, string> PermissionDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PermissionCodes.StoresRead] = "Consultar lojas às quais o usuário possui acesso.",
            [PermissionCodes.StoresManage] = "Criar, alterar, ativar e inativar lojas.",
            [PermissionCodes.UsersRead] = "Consultar usuários dentro do escopo permitido.",
            [PermissionCodes.UsersManage] = "Criar, alterar e redefinir senhas de usuários.",
            [PermissionCodes.RolesRead] = "Consultar perfis e suas permissões.",
            [PermissionCodes.RolesManage] = "Alterar as permissões dos perfis de loja.",
            [PermissionCodes.CategoriesRead] = "Consultar categorias de produtos.",
            [PermissionCodes.CategoriesManage] = "Criar, alterar, ativar e inativar categorias.",
            [PermissionCodes.ProductsRead] = "Consultar o catálogo de produtos.",
            [PermissionCodes.ProductsManage] = "Criar, alterar, ativar e inativar produtos.",
            [PermissionCodes.CustomersRead] = "Consultar clientes.",
            [PermissionCodes.CustomersManage] = "Criar, alterar, ativar e inativar clientes.",
            [PermissionCodes.SuppliersRead] = "Consultar fornecedores.",
            [PermissionCodes.SuppliersManage] = "Criar, alterar, ativar e inativar fornecedores.",
            [PermissionCodes.InventoryRead] = "Consultar posição e histórico de estoque nas lojas permitidas.",
            [PermissionCodes.InventoryManage] = "Registrar ajustes e configurar estoque mínimo nas lojas permitidas."
        };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await EnsureRolesAsync();
        await EnsurePermissionsAsync(cancellationToken);
        await EnsureRolePermissionsAsync(cancellationToken);
        await EnsureCatalogAndPartiesRolePermissionsAsync(cancellationToken);
        await EnsureInventoryRolePermissionsAsync(cancellationToken);
        var stores = await EnsureStoresAsync(cancellationToken);

        if (seedOptions.Value.DemoDataEnabled)
        {
            ValidateDemoPasswords(seedOptions.Value);
            await EnsureDemoUsersAsync(stores, seedOptions.Value, cancellationToken);
            await masterDataSeeder.SeedAsync(cancellationToken);
            await inventorySeeder.SeedAsync(cancellationToken);
            logger.LogInformation("StoreControl demonstration users are ready");
        }
        else
        {
            logger.LogInformation("Demonstration user seed is disabled");
        }
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                IsSystemRole = true
            });
            EnsureSucceeded(result, $"create role {roleName}");
        }
    }

    private async Task EnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Permissions
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var missing = PermissionDescriptions
            .Where(permission => !existingCodes.Contains(permission.Key, StringComparer.Ordinal))
            .Select(permission => new Permission
            {
                Code = permission.Key,
                Description = permission.Value
            });
        dbContext.Permissions.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name!, cancellationToken);
        var permissions = await dbContext.Permissions.ToDictionaryAsync(permission => permission.Code, cancellationToken);

        var defaults = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [RoleNames.Administrator] = PermissionCodes.All,
            [RoleNames.Manager] =
            [
                PermissionCodes.StoresRead,
                PermissionCodes.UsersRead,
                PermissionCodes.RolesRead,
                PermissionCodes.CategoriesRead,
                PermissionCodes.CategoriesManage,
                PermissionCodes.ProductsRead,
                PermissionCodes.ProductsManage,
                PermissionCodes.CustomersRead,
                PermissionCodes.CustomersManage,
                PermissionCodes.SuppliersRead,
                PermissionCodes.SuppliersManage
            ],
            [RoleNames.Seller] =
            [
                PermissionCodes.StoresRead,
                PermissionCodes.CategoriesRead,
                PermissionCodes.ProductsRead,
                PermissionCodes.CustomersRead,
                PermissionCodes.CustomersManage
            ],
            [RoleNames.StockClerk] =
            [
                PermissionCodes.StoresRead,
                PermissionCodes.CategoriesRead,
                PermissionCodes.ProductsRead,
                PermissionCodes.ProductsManage,
                PermissionCodes.SuppliersRead
            ]
        };

        foreach (var (roleName, defaultPermissions) in defaults)
        {
            var role = roles[roleName];
            var existingCount = await dbContext.RolePermissions
                .CountAsync(rolePermission => rolePermission.RoleId == role.Id, cancellationToken);

            // Non-administrator mappings become user-managed after their initial creation.
            var codesToAdd = roleName == RoleNames.Administrator
                ? defaultPermissions
                : existingCount == 0 ? defaultPermissions : [];

            var existingPermissionIds = await dbContext.RolePermissions
                .Where(rolePermission => rolePermission.RoleId == role.Id)
                .Select(rolePermission => rolePermission.PermissionId)
                .ToListAsync(cancellationToken);
            dbContext.RolePermissions.AddRange(codesToAdd
                .Select(code => permissions[code])
                .Where(permission => !existingPermissionIds.Contains(permission.Id))
                .Select(permission => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCatalogAndPartiesRolePermissionsAsync(CancellationToken cancellationToken)
    {
        const string seedKey = "catalog-parties-permissions-v1";
        if (await dbContext.FoundationRecords.AnyAsync(record => record.Key == seedKey, cancellationToken))
        {
            return;
        }

        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name!, cancellationToken);
        var permissions = await dbContext.Permissions.ToDictionaryAsync(permission => permission.Code, cancellationToken);
        var mappings = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [RoleNames.Manager] =
            [
                PermissionCodes.CategoriesRead,
                PermissionCodes.CategoriesManage,
                PermissionCodes.ProductsRead,
                PermissionCodes.ProductsManage,
                PermissionCodes.CustomersRead,
                PermissionCodes.CustomersManage,
                PermissionCodes.SuppliersRead,
                PermissionCodes.SuppliersManage
            ],
            [RoleNames.Seller] =
            [
                PermissionCodes.CategoriesRead,
                PermissionCodes.ProductsRead,
                PermissionCodes.CustomersRead,
                PermissionCodes.CustomersManage
            ],
            [RoleNames.StockClerk] =
            [
                PermissionCodes.CategoriesRead,
                PermissionCodes.ProductsRead,
                PermissionCodes.ProductsManage,
                PermissionCodes.SuppliersRead
            ]
        };
        var affectedRoleIds = mappings.Keys.Select(roleName => roles[roleName].Id).ToArray();

        foreach (var (roleName, permissionCodes) in mappings)
        {
            var roleId = roles[roleName].Id;
            var existingIds = await dbContext.RolePermissions
                .Where(mapping => mapping.RoleId == roleId)
                .Select(mapping => mapping.PermissionId)
                .ToListAsync(cancellationToken);
            dbContext.RolePermissions.AddRange(permissionCodes
                .Select(code => permissions[code])
                .Where(permission => !existingIds.Contains(permission.Id))
                .Select(permission => new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id
                }));
        }

        var affectedUserIds = await dbContext.UserStoreRoles
            .Where(assignment => affectedRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var affectedUsers = await dbContext.Users
            .Where(user => affectedUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);
        foreach (var user in affectedUsers)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        dbContext.FoundationRecords.Add(new FoundationRecord
        {
            Key = seedKey,
            Value = "applied",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, Store>> EnsureStoresAsync(
        CancellationToken cancellationToken)
    {
        var defaults = new[]
        {
            new { Code = "CENTRAL", Name = "Loja Central" },
            new { Code = "NORTE", Name = "Loja Norte" },
            new { Code = "SUL", Name = "Loja Sul" }
        };
        var existingCodes = await dbContext.Stores.Select(store => store.Code).ToListAsync(cancellationToken);
        dbContext.Stores.AddRange(defaults
            .Where(item => !existingCodes.Contains(item.Code, StringComparer.Ordinal))
            .Select(item => new Store(item.Code, item.Name)));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Stores.ToDictionaryAsync(store => store.Code, cancellationToken);
    }

    private async Task EnsureInventoryRolePermissionsAsync(CancellationToken cancellationToken)
    {
        const string seedKey = "inventory-permissions-v1";
        if (await dbContext.FoundationRecords.AnyAsync(record => record.Key == seedKey, cancellationToken))
        {
            return;
        }

        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name!, cancellationToken);
        var permissions = await dbContext.Permissions.ToDictionaryAsync(permission => permission.Code, cancellationToken);
        var mappings = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [RoleNames.Manager] = [PermissionCodes.InventoryRead, PermissionCodes.InventoryManage],
            [RoleNames.Seller] = [PermissionCodes.InventoryRead],
            [RoleNames.StockClerk] = [PermissionCodes.InventoryRead, PermissionCodes.InventoryManage]
        };
        var affectedRoleIds = mappings.Keys.Select(roleName => roles[roleName].Id).ToArray();

        foreach (var (roleName, permissionCodes) in mappings)
        {
            var roleId = roles[roleName].Id;
            var existingIds = await dbContext.RolePermissions
                .Where(mapping => mapping.RoleId == roleId)
                .Select(mapping => mapping.PermissionId)
                .ToListAsync(cancellationToken);
            dbContext.RolePermissions.AddRange(permissionCodes
                .Select(code => permissions[code])
                .Where(permission => !existingIds.Contains(permission.Id))
                .Select(permission => new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id
                }));
        }

        var affectedUserIds = await dbContext.UserStoreRoles
            .Where(assignment => affectedRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var affectedUsers = await dbContext.Users
            .Where(user => affectedUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);
        foreach (var user in affectedUsers)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        dbContext.FoundationRecords.Add(new FoundationRecord
        {
            Key = seedKey,
            Value = "applied",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoUsersAsync(
        IReadOnlyDictionary<string, Store> stores,
        DemoSeedOptions options,
        CancellationToken cancellationToken)
    {
        await EnsureUserAsync(
            "Administrador Demo",
            "admin@storecontrol.local",
            options.AdminPassword,
            true,
            [],
            stores,
            cancellationToken);
        await EnsureUserAsync(
            "Gerente Demo",
            "gerente@storecontrol.local",
            options.UserPassword,
            false,
            [("CENTRAL", RoleNames.Manager)],
            stores,
            cancellationToken);
        await EnsureUserAsync(
            "Vendedor Demo",
            "vendedor@storecontrol.local",
            options.UserPassword,
            false,
            [("CENTRAL", RoleNames.Seller)],
            stores,
            cancellationToken);
        await EnsureUserAsync(
            "Estoquista Demo",
            "estoquista@storecontrol.local",
            options.UserPassword,
            false,
            [("CENTRAL", RoleNames.StockClerk), ("NORTE", RoleNames.StockClerk)],
            stores,
            cancellationToken);
    }

    private async Task EnsureUserAsync(
        string fullName,
        string email,
        string password,
        bool administrator,
        IReadOnlyCollection<(string StoreCode, string RoleName)> assignments,
        IReadOnlyDictionary<string, Store> stores,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), $"create demo user {email}");
        }

        if (administrator && !await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, RoleNames.Administrator),
                $"assign administrator role to {email}");
        }

        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name!, cancellationToken);
        foreach (var (storeCode, roleName) in assignments)
        {
            var store = stores[storeCode];
            var existing = await dbContext.UserStoreRoles
                .SingleOrDefaultAsync(
                    assignment => assignment.UserId == user.Id && assignment.StoreId == store.Id,
                    cancellationToken);
            if (existing is null)
            {
                dbContext.UserStoreRoles.Add(new UserStoreRole
                {
                    UserId = user.Id,
                    StoreId = store.Id,
                    RoleId = roles[roleName].Id
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDemoPasswords(DemoSeedOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminPassword) ||
            string.IsNullOrWhiteSpace(options.UserPassword))
        {
            throw new InvalidOperationException(
                "Seed:AdminPassword and Seed:UserPassword are required when demonstration data is enabled.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity seed operation '{operation}' failed: " +
                string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}")));
        }
    }
}
