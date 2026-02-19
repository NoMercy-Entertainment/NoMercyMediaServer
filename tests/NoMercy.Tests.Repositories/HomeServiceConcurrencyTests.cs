using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class HomeServiceConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<MediaContext> _factory;

    public HomeServiceConcurrencyTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateFactory();

        using MediaContext seedContext = _factory.CreateDbContext();
        TestMediaContextFactory.SeedData(seedContext);
    }

    [Fact]
    public async Task GetHomeData_WithParallelQueries_DoesNotThrow()
    {
        MediaContext mainContext = _factory.CreateDbContext();
        HomeRepository homeRepository = new();
        LibraryRepository libraryRepository = new(mainContext);
        HomeService service = new(homeRepository, libraryRepository, mainContext, _factory);

        Exception? exception = await Record.ExceptionAsync(async () =>
        {
            await service.GetHomeData(SeedConstants.UserId, "en", "US");
        });

        Assert.Null(exception);
        await mainContext.DisposeAsync();
    }

    [Fact]
    public async Task GetHomeData_CalledMultipleTimes_DoesNotThrow()
    {
        MediaContext mainContext = _factory.CreateDbContext();
        HomeRepository homeRepository = new();
        LibraryRepository libraryRepository = new(mainContext);
        HomeService service = new(homeRepository, libraryRepository, mainContext, _factory);

        for (int i = 0; i < 3; i++)
        {
            Exception? exception = await Record.ExceptionAsync(async () =>
            {
                await service.GetHomeData(SeedConstants.UserId, "en", "US");
            });

            Assert.Null(exception);
        }

        await mainContext.DisposeAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
