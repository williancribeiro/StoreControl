namespace StoreControl.Application.Abstractions.Persistence;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
