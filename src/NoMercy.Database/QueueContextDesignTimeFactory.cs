using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NoMercy.Database;

/// <summary>
/// Design-time factory for QueueContext, used by EF Core migrations tooling.
/// This allows migrations to be generated without running the full application.
/// </summary>
public class QueueContextDesignTimeFactory : IDesignTimeDbContextFactory<QueueContext>
{
    public QueueContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();

        // Use a temporary database path for design-time operations
        string dbPath = Path.Combine(Path.GetTempPath(), "nomercy_queue_design.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new QueueContext(optionsBuilder.Options);
    }
}
