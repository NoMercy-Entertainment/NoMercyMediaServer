using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Libraries;

public class LibraryManagerEventTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MediaContext _context;

    public LibraryManagerEventTests()
    {
        string dbName = Guid.NewGuid().ToString();
        _connection = new SqliteConnection($"DataSource={dbName};Mode=Memory;Cache=Shared");
        _connection.Open();
        _connection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(_connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

        _context = new MediaContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessLibrary_NonExistentLibrary_DoesNotPublishEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>((e, _) =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });
        bus.Subscribe<LibraryScanCompletedEvent>((e, _) =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        LibraryRepository repo = new(_context);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(repo, dispatcher, _context, bus);

        await manager.ProcessLibrary(Ulid.NewUlid());

        Assert.Empty(received);
    }

    [Fact]
    public async Task ProcessLibrary_EmptyLibrary_PublishesStartAndCompletedEvents()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<LibraryScanStartedEvent>((e, _) =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });
        bus.Subscribe<LibraryScanCompletedEvent>((e, _) =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "Test Movies",
            Type = "movie"
        });
        await _context.SaveChangesAsync();

        LibraryRepository repo = new(_context);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(repo, dispatcher, _context, bus);

        await manager.ProcessLibrary(libraryId);

        Assert.Equal(2, received.Count);

        LibraryScanStartedEvent started = Assert.IsType<LibraryScanStartedEvent>(received[0]);
        Assert.Equal(libraryId, started.LibraryId);
        Assert.Equal("Test Movies", started.LibraryName);

        LibraryScanCompletedEvent completed = Assert.IsType<LibraryScanCompletedEvent>(received[1]);
        Assert.Equal(libraryId, completed.LibraryId);
        Assert.Equal("Test Movies", completed.LibraryName);
        Assert.Equal(0, completed.ItemsFound);
        Assert.True(completed.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessLibrary_WithoutEventBus_DoesNotThrow()
    {
        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "No Events Library",
            Type = "movie"
        });
        await _context.SaveChangesAsync();

        LibraryRepository repo = new(_context);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(repo, dispatcher, _context);

        await manager.ProcessLibrary(libraryId);
    }

    [Fact]
    public async Task ProcessLibrary_CompletedEvent_HasValidDuration()
    {
        InMemoryEventBus bus = new();
        LibraryScanCompletedEvent? completedEvent = null;

        bus.Subscribe<LibraryScanCompletedEvent>((e, _) =>
        {
            completedEvent = e;
            return Task.CompletedTask;
        });

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "Duration Test",
            Type = "tv"
        });
        await _context.SaveChangesAsync();

        LibraryRepository repo = new(_context);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(repo, dispatcher, _context, bus);

        await manager.ProcessLibrary(libraryId);

        Assert.NotNull(completedEvent);
        Assert.True(completedEvent.Duration >= TimeSpan.Zero);
        Assert.True(completedEvent.Duration < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ProcessLibrary_StartedEvent_HasCorrectEventMetadata()
    {
        InMemoryEventBus bus = new();
        LibraryScanStartedEvent? startedEvent = null;

        bus.Subscribe<LibraryScanStartedEvent>((e, _) =>
        {
            startedEvent = e;
            return Task.CompletedTask;
        });

        Ulid libraryId = Ulid.NewUlid();
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "Metadata Test",
            Type = "movie"
        });
        await _context.SaveChangesAsync();

        LibraryRepository repo = new(_context);
        JobDispatcher dispatcher = new();
        LibraryManager manager = new(repo, dispatcher, _context, bus);

        await manager.ProcessLibrary(libraryId);

        Assert.NotNull(startedEvent);
        Assert.NotEqual(Guid.Empty, startedEvent.EventId);
        Assert.True(startedEvent.Timestamp <= DateTime.UtcNow);
        Assert.Equal("LibraryScanner", startedEvent.Source);
    }
}
