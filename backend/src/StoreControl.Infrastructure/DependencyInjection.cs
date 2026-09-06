using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Application.Administration;
using StoreControl.Application.Abstractions.Persistence;
using StoreControl.Application.Catalog;
using StoreControl.Application.Cash;
using StoreControl.Application.Inventory;
using StoreControl.Application.Purchasing;
using StoreControl.Application.Reporting;
using StoreControl.Application.Sales;
using StoreControl.Application.Parties;
using StoreControl.Application.Security;
using StoreControl.Infrastructure.Administration;
using StoreControl.Infrastructure.Catalog;
using StoreControl.Infrastructure.Caching;
using StoreControl.Infrastructure.Cash;
using StoreControl.Infrastructure.HealthChecks;
using StoreControl.Infrastructure.Identity;
using StoreControl.Infrastructure.Inventory;
using StoreControl.Infrastructure.Messaging;
using StoreControl.Infrastructure.Purchasing;
using StoreControl.Infrastructure.Reporting;
using StoreControl.Infrastructure.Sales;
using StoreControl.Infrastructure.Parties;
using StoreControl.Infrastructure.Persistence;
using StoreControl.Infrastructure.Persistence.Seeding;

namespace StoreControl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("StoreControl");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A connection string 'StoreControl' não foi configurada. Consulte o arquivo .env.example.");
        }

        services.AddDbContext<StoreControlDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(typeof(StoreControlDbContext).Assembly.FullName);
                sqlServer.EnableRetryOnFailure(maxRetryCount: 5);
            }));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<StoreControlDbContext>();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(
                options => options.LifetimeDays is >= 1 and <= 90,
                "Authentication:RefreshToken:LifetimeDays must be between 1 and 90.")
            .ValidateOnStart();
        services.Configure<DemoSeedOptions>(
            configuration.GetSection(DemoSeedOptions.SectionName));

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection("Cache"))
            .Validate(options => options.IsValid(), "Invalid Cache configuration; check enabled connection, prefix, TTLs and timeout.")
            .ValidateOnStart();
        services.AddSingleton<RedisConnection>();
        services.AddSingleton<CacheTelemetry>();
        services.AddScoped<RedisReadCache>();

        services.AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Invalid Messaging configuration; check credentials, topology prefix, timeouts and retry delays.")
            .ValidateOnStart();
        services.AddSingleton(serviceProvider =>
            new RabbitTopology(serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>().Value));
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddScoped<OutboxStore>();

        services.AddScoped<IDatabaseSmokeCheck, SqlServerDatabaseSmokeCheck>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<MasterDataSeeder>();
        services.AddScoped<InventorySeeder>();
        services.AddScoped<PurchaseSeeder>();
        services.AddScoped<CashRegisterSeeder>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ITokenSessionValidator, TokenSessionValidator>();
        services.AddScoped<IStoreAdministrationService, StoreAdministrationService>();
        services.AddScoped<IRoleAdministrationService, RoleAdministrationService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["cache"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["messaging"]);

        return services;
    }

    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services)
    {
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
