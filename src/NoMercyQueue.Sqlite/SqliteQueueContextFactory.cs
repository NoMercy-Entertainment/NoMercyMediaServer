using Microsoft.EntityFrameworkCore;
using NoMercyQueue.Core.Interfaces;

namespace NoMercyQueue.Sqlite;

public static class SqliteQueueContextFactory
{
    public static IQueueContext Create(string databasePath)
    {
        DbContextOptions<QueueDbContext> options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseSqlite(
                $"Data Source={databasePath}; Pooling=True; Foreign Keys=True;"
            )
            .AddInterceptors(new SqliteQueueConnectionInterceptor())
            .Options;

        QueueDbContext dbContext = new(options);
        dbContext.Database.EnsureCreated();

        return new SqliteQueueContext(dbContext);
    }
}
