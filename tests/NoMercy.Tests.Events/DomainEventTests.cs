using FluentAssertions;
using NoMercy.Events;
using NoMercy.Events.Configuration;
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Events.Playback;
using NoMercy.Events.Plugins;
using NoMercy.Events.Users;
using Xunit;

namespace NoMercy.Tests.Events;

public class DomainEventTests
{
    [Fact]
    public void MediaDiscoveredEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaDiscoveredEvent evt = new()
        {
            FilePath = "/media/movies/test.mkv",
            LibraryId = libraryId,
            DetectedType = "movie"
        };

        evt.Source.Should().Be("MediaScanner");
        evt.FilePath.Should().Be("/media/movies/test.mkv");
        evt.LibraryId.Should().Be(libraryId);
        evt.DetectedType.Should().Be("movie");
        evt.EventId.Should().NotBe(Guid.Empty);
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MediaDiscoveredEvent_DetectedTypeIsOptional()
    {
        MediaDiscoveredEvent evt = new()
        {
            FilePath = "/media/test.mkv",
            LibraryId = Ulid.NewUlid()
        };

        evt.DetectedType.Should().BeNull();
    }

    [Fact]
    public void MediaAddedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaAddedEvent evt = new()
        {
            MediaId = 12345,
            MediaType = "movie",
            Title = "Test Movie",
            LibraryId = libraryId
        };

        evt.Source.Should().Be("MediaProcessor");
        evt.MediaId.Should().Be(12345);
        evt.MediaType.Should().Be("movie");
        evt.Title.Should().Be("Test Movie");
        evt.LibraryId.Should().Be(libraryId);
    }

    [Fact]
    public void MediaRemovedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaRemovedEvent evt = new()
        {
            MediaId = 99,
            MediaType = "tv",
            Title = "Test Show",
            LibraryId = libraryId
        };

        evt.Source.Should().Be("MediaProcessor");
        evt.MediaId.Should().Be(99);
        evt.MediaType.Should().Be("tv");
        evt.Title.Should().Be("Test Show");
        evt.LibraryId.Should().Be(libraryId);
    }

    [Fact]
    public void EncodingStartedEvent_SetsAllProperties()
    {
        EncodingStartedEvent evt = new()
        {
            JobId = 42,
            InputPath = "/input/video.mkv",
            OutputPath = "/output/video/",
            ProfileName = "HLS-1080p"
        };

        evt.Source.Should().Be("Encoder");
        evt.JobId.Should().Be(42);
        evt.InputPath.Should().Be("/input/video.mkv");
        evt.OutputPath.Should().Be("/output/video/");
        evt.ProfileName.Should().Be("HLS-1080p");
    }

    [Fact]
    public void EncodingProgressEvent_SetsAllProperties()
    {
        EncodingProgressEvent evt = new()
        {
            JobId = 42,
            Percentage = 55.5,
            Elapsed = TimeSpan.FromMinutes(10),
            Estimated = TimeSpan.FromMinutes(8)
        };

        evt.Source.Should().Be("Encoder");
        evt.JobId.Should().Be(42);
        evt.Percentage.Should().Be(55.5);
        evt.Elapsed.Should().Be(TimeSpan.FromMinutes(10));
        evt.Estimated.Should().Be(TimeSpan.FromMinutes(8));
    }

    [Fact]
    public void EncodingProgressEvent_EstimatedIsOptional()
    {
        EncodingProgressEvent evt = new()
        {
            JobId = 1,
            Percentage = 0.0,
            Elapsed = TimeSpan.Zero
        };

        evt.Estimated.Should().BeNull();
    }

    [Fact]
    public void EncodingCompletedEvent_SetsAllProperties()
    {
        EncodingCompletedEvent evt = new()
        {
            JobId = 42,
            OutputPath = "/output/video/playlist.m3u8",
            Duration = TimeSpan.FromMinutes(18)
        };

        evt.Source.Should().Be("Encoder");
        evt.JobId.Should().Be(42);
        evt.OutputPath.Should().Be("/output/video/playlist.m3u8");
        evt.Duration.Should().Be(TimeSpan.FromMinutes(18));
    }

    [Fact]
    public void EncodingFailedEvent_SetsAllProperties()
    {
        EncodingFailedEvent evt = new()
        {
            JobId = 42,
            InputPath = "/input/corrupt.mkv",
            ErrorMessage = "FFmpeg exited with code 1",
            ExceptionType = "InvalidOperationException"
        };

        evt.Source.Should().Be("Encoder");
        evt.JobId.Should().Be(42);
        evt.InputPath.Should().Be("/input/corrupt.mkv");
        evt.ErrorMessage.Should().Be("FFmpeg exited with code 1");
        evt.ExceptionType.Should().Be("InvalidOperationException");
    }

    [Fact]
    public void EncodingFailedEvent_ExceptionTypeIsOptional()
    {
        EncodingFailedEvent evt = new()
        {
            JobId = 1,
            InputPath = "/input/test.mkv",
            ErrorMessage = "Unknown error"
        };

        evt.ExceptionType.Should().BeNull();
    }

    [Fact]
    public void UserAuthenticatedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        UserAuthenticatedEvent evt = new()
        {
            UserId = userId,
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        evt.Source.Should().Be("Auth");
        evt.UserId.Should().Be(userId);
        evt.Email.Should().Be("user@example.com");
        evt.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public void UserDisconnectedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        UserDisconnectedEvent evt = new()
        {
            UserId = userId,
            ConnectionId = "abc-123-def"
        };

        evt.Source.Should().Be("SignalR");
        evt.UserId.Should().Be(userId);
        evt.ConnectionId.Should().Be("abc-123-def");
    }

    [Fact]
    public void PlaybackStartedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackStartedEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            MediaType = "movie",
            DeviceId = "device-001"
        };

        evt.Source.Should().Be("Playback");
        evt.UserId.Should().Be(userId);
        evt.MediaId.Should().Be(500);
        evt.MediaType.Should().Be("movie");
        evt.DeviceId.Should().Be("device-001");
    }

    [Fact]
    public void PlaybackStartedEvent_DeviceIdIsOptional()
    {
        PlaybackStartedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "tv"
        };

        evt.DeviceId.Should().BeNull();
    }

    [Fact]
    public void PlaybackProgressEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackProgressEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            Position = TimeSpan.FromMinutes(45),
            Duration = TimeSpan.FromMinutes(120)
        };

        evt.Source.Should().Be("Playback");
        evt.UserId.Should().Be(userId);
        evt.MediaId.Should().Be(500);
        evt.Position.Should().Be(TimeSpan.FromMinutes(45));
        evt.Duration.Should().Be(TimeSpan.FromMinutes(120));
    }

    [Fact]
    public void PlaybackCompletedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackCompletedEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            MediaType = "movie"
        };

        evt.Source.Should().Be("Playback");
        evt.UserId.Should().Be(userId);
        evt.MediaId.Should().Be(500);
        evt.MediaType.Should().Be("movie");
    }

    [Fact]
    public void LibraryScanStartedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        LibraryScanStartedEvent evt = new()
        {
            LibraryId = libraryId,
            LibraryName = "Movies"
        };

        evt.Source.Should().Be("LibraryScanner");
        evt.LibraryId.Should().Be(libraryId);
        evt.LibraryName.Should().Be("Movies");
    }

    [Fact]
    public void LibraryScanCompletedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        LibraryScanCompletedEvent evt = new()
        {
            LibraryId = libraryId,
            LibraryName = "Movies",
            ItemsFound = 150,
            Duration = TimeSpan.FromSeconds(30)
        };

        evt.Source.Should().Be("LibraryScanner");
        evt.LibraryId.Should().Be(libraryId);
        evt.LibraryName.Should().Be("Movies");
        evt.ItemsFound.Should().Be(150);
        evt.Duration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void PluginLoadedEvent_SetsAllProperties()
    {
        PluginLoadedEvent evt = new()
        {
            PluginId = "my-plugin",
            PluginName = "My Plugin",
            Version = "1.0.0"
        };

        evt.Source.Should().Be("PluginManager");
        evt.PluginId.Should().Be("my-plugin");
        evt.PluginName.Should().Be("My Plugin");
        evt.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void PluginErrorEvent_SetsAllProperties()
    {
        PluginErrorEvent evt = new()
        {
            PluginId = "bad-plugin",
            PluginName = "Bad Plugin",
            ErrorMessage = "Failed to initialize",
            ExceptionType = "NullReferenceException"
        };

        evt.Source.Should().Be("PluginManager");
        evt.PluginId.Should().Be("bad-plugin");
        evt.PluginName.Should().Be("Bad Plugin");
        evt.ErrorMessage.Should().Be("Failed to initialize");
        evt.ExceptionType.Should().Be("NullReferenceException");
    }

    [Fact]
    public void PluginErrorEvent_ExceptionTypeIsOptional()
    {
        PluginErrorEvent evt = new()
        {
            PluginId = "x",
            PluginName = "X",
            ErrorMessage = "error"
        };

        evt.ExceptionType.Should().BeNull();
    }

    [Fact]
    public void ConfigurationChangedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        ConfigurationChangedEvent evt = new()
        {
            Section = "Encoding",
            Key = "DefaultProfile",
            ChangedByUserId = userId
        };

        evt.Source.Should().Be("Configuration");
        evt.Section.Should().Be("Encoding");
        evt.Key.Should().Be("DefaultProfile");
        evt.ChangedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ConfigurationChangedEvent_ChangedByUserIdIsOptional()
    {
        ConfigurationChangedEvent evt = new()
        {
            Section = "System",
            Key = "Port"
        };

        evt.ChangedByUserId.Should().BeNull();
    }

    [Fact]
    public void AllDomainEvents_ImplementIEvent()
    {
        IEvent[] events =
        [
            new MediaDiscoveredEvent { FilePath = "/test", LibraryId = Ulid.NewUlid() },
            new MediaAddedEvent { MediaId = 1, MediaType = "movie", Title = "T", LibraryId = Ulid.NewUlid() },
            new MediaRemovedEvent { MediaId = 1, MediaType = "movie", Title = "T", LibraryId = Ulid.NewUlid() },
            new EncodingStartedEvent { JobId = 1, InputPath = "/i", OutputPath = "/o", ProfileName = "p" },
            new EncodingProgressEvent { JobId = 1, Percentage = 0, Elapsed = TimeSpan.Zero },
            new EncodingCompletedEvent { JobId = 1, OutputPath = "/o", Duration = TimeSpan.Zero },
            new EncodingFailedEvent { JobId = 1, InputPath = "/i", ErrorMessage = "e" },
            new UserAuthenticatedEvent { UserId = Guid.NewGuid(), Email = "a@b.c", DisplayName = "A" },
            new UserDisconnectedEvent { UserId = Guid.NewGuid(), ConnectionId = "c" },
            new PlaybackStartedEvent { UserId = Guid.NewGuid(), MediaId = 1, MediaType = "movie" },
            new PlaybackProgressEvent { UserId = Guid.NewGuid(), MediaId = 1, Position = TimeSpan.Zero, Duration = TimeSpan.Zero },
            new PlaybackCompletedEvent { UserId = Guid.NewGuid(), MediaId = 1, MediaType = "movie" },
            new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "L" },
            new LibraryScanCompletedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "L", ItemsFound = 0, Duration = TimeSpan.Zero },
            new PluginLoadedEvent { PluginId = "p", PluginName = "P", Version = "1.0" },
            new PluginErrorEvent { PluginId = "p", PluginName = "P", ErrorMessage = "e" },
            new ConfigurationChangedEvent { Section = "s", Key = "k" }
        ];

        foreach (IEvent evt in events)
        {
            evt.EventId.Should().NotBe(Guid.Empty);
            evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            evt.Source.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task AllDomainEvents_CanBePublishedViaEventBus()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<MediaDiscoveredEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<MediaAddedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<LibraryScanCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        await bus.PublishAsync(new MediaDiscoveredEvent { FilePath = "/test", LibraryId = Ulid.NewUlid() });
        await bus.PublishAsync(new MediaAddedEvent { MediaId = 1, MediaType = "movie", Title = "T", LibraryId = Ulid.NewUlid() });
        await bus.PublishAsync(new EncodingStartedEvent { JobId = 1, InputPath = "/i", OutputPath = "/o", ProfileName = "p" });
        await bus.PublishAsync(new LibraryScanCompletedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "L", ItemsFound = 5, Duration = TimeSpan.FromSeconds(1) });

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<MediaDiscoveredEvent>();
        received[1].Should().BeOfType<MediaAddedEvent>();
        received[2].Should().BeOfType<EncodingStartedEvent>();
        received[3].Should().BeOfType<LibraryScanCompletedEvent>();
    }
}
