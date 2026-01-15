using Microsoft.EntityFrameworkCore;
using NoMercy.Database;

namespace NoMercy.Tests.Queue.TestHelpers;

public class TestQueueContext : QueueContext
{
    public TestQueueContext(DbContextOptions<QueueContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Don't configure anything if options are already provided
        if (!options.IsConfigured)
        {
            base.OnConfiguring(options);
        }
    }
}

public static class TestQueueContextFactory
{
    public static QueueContext CreateInMemoryContext(string databaseName = "TestDatabase")
    {
        DbContextOptions<QueueContext> options = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(databaseName: $"{databaseName}_{Guid.NewGuid()}")
            .Options;

        TestQueueContext context = new(options);
        context.Database.EnsureCreated();
        return context;
    }
}
