using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Storage;
using NoMercy.Service.Seeds;

namespace NoMercy.Tests.Setup;

[Trait("Category", "Unit")]
public class SystemLocalDriverSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public SystemLocalDriverSeedTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                _connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext CreateContext() => new(_options);

    [Fact]
    public async Task SeedSystemLocalDriver_InsertsSingleRow()
    {
        using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(ctx);

        int count = await ctx.Drivers.CountAsync(d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_IsIdempotent_RunTwice_StillOneRow()
    {
        using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(ctx);
        await DatabaseSeeder.SeedSystemLocalDriver(ctx);

        int count = await ctx.Drivers.CountAsync(d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_RowHasExpectedValues()
    {
        using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(ctx);

        Driver? driver = await ctx.Drivers.FindAsync(Driver.SystemLocalDriverId);
        Assert.NotNull(driver);
        Assert.Equal("Local", driver.Name);
        Assert.Equal("local", driver.Type);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_DoesNotInsertWhenAlreadyPresent()
    {
        using MediaContext ctx = CreateContext();

        // Pre-insert a row with the same id to simulate an existing install.
        ctx.Drivers.Add(
            new Driver
            {
                Id = Driver.SystemLocalDriverId,
                Name = "Local",
                Type = "local",
                Config = "{\"rootPath\":\"\"}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        await ctx.SaveChangesAsync();

        // Should not throw a unique-constraint violation.
        await DatabaseSeeder.SeedSystemLocalDriver(ctx);

        int count = await ctx.Drivers.CountAsync(d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(1, count);
    }

    [Fact]
    public void SystemLocalDriverId_IsStable()
    {
        // The id must never change between builds — clients rely on it.
        Ulid id = Driver.SystemLocalDriverId;
        Assert.Equal("01JKQSTS00000000000000000A", id.ToString());
    }
}
