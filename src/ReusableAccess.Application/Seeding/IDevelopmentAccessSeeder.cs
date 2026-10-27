namespace ReusableAccess.Application.Seeding;

public interface IDevelopmentAccessSeeder
{
    Task SeedAsync(Guid userId);
}