using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Persistence;

namespace Sensei.Host.Persistence;

internal static class DevelopmentDataSeeder
{
    internal static readonly Guid DevelopmentOwnerId = Guid.Parse("61c9ed1a-2333-46a2-9d48-3a840228e61b");

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO identity.users
                (id, email, display_name, ui_locale, answer_language, time_zone, created_at, version)
            VALUES
                ({{DevelopmentOwnerId}}, 'developer@sensei.local', 'Local Developer', 'en', 'en', 'Europe/Minsk', {{DateTimeOffset.UtcNow}}, 1)
            ON CONFLICT (id) DO NOTHING;
            """);
    }
}
