using Microsoft.EntityFrameworkCore;
using NoMercy.Queue.Core.Interfaces;

namespace NoMercy.Queue.Sqlite;

public static class SqliteQueueContextFactory
{
    public static IQueueContext Create(string databasePath)
    {
        DbContextOptions<QueueDbContext> options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseSqlite($"Data Source={databasePath}; Pooling=True; Cache=Shared; Foreign Keys=True;")
            .Options;

        QueueDbContext dbContext = new(options);
        dbContext.Database.EnsureCreated();

        return new SqliteQueueContext(dbContext);
    }
}
