using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginAbstractionsTests
{
    private sealed class TestPlugin : IPlugin
    {
        public string Name => "Test Plugin";
        public string Description => "A test plugin for unit testing";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);
        public bool Initialized { get; private set; }
        public IPluginContext? ReceivedContext { get; private set; }

        public void Initialize(IPluginContext context)
        {
            ReceivedContext = context;
            Initialized = true;
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestMetadataPlugin : IMetadataPlugin
    {
        public string Name => "Test Metadata";
        public string Description => "Test metadata provider";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);

        public void Initialize(IPluginContext context) { }

        public Task<MediaMetadata?> GetMetadataAsync(string title, MediaType type, CancellationToken ct = default)
        {
            return Task.FromResult<MediaMetadata?>(new MediaMetadata
            {
                Title = title,
                Year = 2024,
                Genres = ["Action"]
            });
        }

        public void Dispose() { }
    }

    private sealed class TestMediaSourcePlugin : IMediaSourcePlugin
    {
        public string Name => "Test Source";
        public string Description => "Test media source";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);

        public void Initialize(IPluginContext context) { }

        public Task<IEnumerable<MediaFile>> ScanAsync(string path, CancellationToken ct = default)
        {
            return Task.FromResult<IEnumerable<MediaFile>>(
            [
                new MediaFile { Path = $"{path}/movie.mkv", FileName = "movie.mkv", Size = 1024, Type = MediaType.Movie }
            ]);
        }

        public void Dispose() { }
    }

    private sealed class TestEncoderPlugin : IEncoderPlugin
    {
        public string Name => "Test Encoder";
        public string Description => "Test encoder";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);

        public void Initialize(IPluginContext context) { }

        public EncodingProfile GetProfile(MediaInfo info)
        {
            return new EncodingProfile
            {
                Name = "test-profile",
                VideoCodec = "libx264",
                AudioCodec = "aac"
            };
        }

        public void Dispose() { }
    }

    private sealed class TestScheduledPlugin : IScheduledTaskPlugin
    {
        public string Name => "Test Scheduled";
        public string Description => "Test scheduled task";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);
        public string CronExpression => "0 0 * * *";
        public bool Executed { get; private set; }

        public void Initialize(IPluginContext context) { }

        public Task ExecuteAsync(CancellationToken ct = default)
        {
            Executed = true;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private sealed class TestAuthPlugin : IAuthPlugin
    {
        public string Name => "Test Auth";
        public string Description => "Test auth";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0, 0);

        public void Initialize(IPluginContext context) { }

        public Task<AuthResult> AuthenticateAsync(string token, CancellationToken ct = default)
        {
            return Task.FromResult(new AuthResult
            {
                IsAuthenticated = token == "valid-token",
                UserName = token == "valid-token" ? "testuser" : null
            });
        }

        public void Dispose() { }
    }

    private sealed class TestPluginContext : IPluginContext
    {
        public IEventBus EventBus { get; }
        public IServiceProvider Services { get; }
        public ILogger Logger { get; }
        public string DataFolderPath { get; }
        public IPluginConfiguration Configuration { get; }

        public TestPluginContext(IEventBus eventBus, string dataFolder = "/tmp/plugin-test")
        {
            EventBus = eventBus;
            Services = new MinimalServiceProvider();
            Logger = NullLogger.Instance;
            DataFolderPath = dataFolder;
            Configuration = new NullPluginConfiguration();
        }

        private sealed class MinimalServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        private sealed class NullPluginConfiguration : IPluginConfiguration
        {
            public T? GetConfiguration<T>() where T : class, new() => null;
            public Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default) where T : class, new() => Task.FromResult<T?>(null);
            public void SaveConfiguration<T>(T configuration) where T : class { }
            public Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default) where T : class => Task.CompletedTask;
            public bool HasConfiguration() => false;
            public void DeleteConfiguration() { }
        }
    }

    [Fact]
    public void IPlugin_CanBeImplemented_WithRequiredProperties()
    {
        using TestPlugin plugin = new();

        plugin.Name.Should().Be("Test Plugin");
        plugin.Description.Should().NotBeNullOrEmpty();
        plugin.Id.Should().NotBe(Guid.Empty);
        plugin.Version.Should().Be(new Version(1, 0, 0));
    }

    [Fact]
    public void IPlugin_Initialize_ReceivesPluginContext()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(bus);

        using TestPlugin plugin = new();
        plugin.Initialize(context);

        plugin.Initialized.Should().BeTrue();
        plugin.ReceivedContext.Should().BeSameAs(context);
    }

    [Fact]
    public void IPluginContext_ProvidesEventBus()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(bus);

        context.EventBus.Should().BeSameAs(bus);
    }

    [Fact]
    public void IPluginContext_ProvidesLogger()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(bus);

        context.Logger.Should().NotBeNull();
    }

    [Fact]
    public void IPluginContext_ProvidesDataFolderPath()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(bus, "/data/plugins/my-plugin");

        context.DataFolderPath.Should().Be("/data/plugins/my-plugin");
    }

    [Fact]
    public async Task IMetadataPlugin_GetMetadataAsync_ReturnsMetadata()
    {
        using TestMetadataPlugin plugin = new();

        MediaMetadata? result = await plugin.GetMetadataAsync("Test Movie", MediaType.Movie);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Movie");
        result.Year.Should().Be(2024);
        result.Genres.Should().Contain("Action");
    }

    [Fact]
    public async Task IMediaSourcePlugin_ScanAsync_ReturnsFiles()
    {
        using TestMediaSourcePlugin plugin = new();

        List<MediaFile> files = (await plugin.ScanAsync("/movies")).ToList();

        files.Should().ContainSingle();
        files[0].FileName.Should().Be("movie.mkv");
        files[0].Type.Should().Be(MediaType.Movie);
        files[0].Size.Should().Be(1024);
    }

    [Fact]
    public void IEncoderPlugin_GetProfile_ReturnsEncodingProfile()
    {
        using TestEncoderPlugin plugin = new();

        MediaInfo info = new() { FilePath = "/test.mkv" };
        EncodingProfile profile = plugin.GetProfile(info);

        profile.Name.Should().Be("test-profile");
        profile.VideoCodec.Should().Be("libx264");
        profile.AudioCodec.Should().Be("aac");
    }

    [Fact]
    public async Task IScheduledTaskPlugin_ExecuteAsync_RunsTask()
    {
        using TestScheduledPlugin plugin = new();

        plugin.CronExpression.Should().Be("0 0 * * *");
        plugin.Executed.Should().BeFalse();

        await plugin.ExecuteAsync();

        plugin.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task IAuthPlugin_AuthenticateAsync_ValidToken_ReturnsAuthenticated()
    {
        using TestAuthPlugin plugin = new();

        AuthResult result = await plugin.AuthenticateAsync("valid-token");

        result.IsAuthenticated.Should().BeTrue();
        result.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task IAuthPlugin_AuthenticateAsync_InvalidToken_ReturnsNotAuthenticated()
    {
        using TestAuthPlugin plugin = new();

        AuthResult result = await plugin.AuthenticateAsync("bad-token");

        result.IsAuthenticated.Should().BeFalse();
        result.UserName.Should().BeNull();
    }

    [Fact]
    public void PluginInfo_HoldsPluginMetadata()
    {
        PluginInfo info = new()
        {
            Id = Guid.NewGuid(),
            Name = "My Plugin",
            Description = "A useful plugin",
            Version = new Version(2, 1, 0),
            Status = PluginStatus.Active,
            Author = "Test Author"
        };

        info.Name.Should().Be("My Plugin");
        info.Status.Should().Be(PluginStatus.Active);
        info.Version.Should().Be(new Version(2, 1, 0));
        info.Author.Should().Be("Test Author");
    }

    [Fact]
    public void PluginStatus_HasAllExpectedValues()
    {
        Enum.GetValues<PluginStatus>().Should().HaveCount(4);
        Enum.GetValues<PluginStatus>().Should().Contain(PluginStatus.Active);
        Enum.GetValues<PluginStatus>().Should().Contain(PluginStatus.Disabled);
        Enum.GetValues<PluginStatus>().Should().Contain(PluginStatus.Malfunctioned);
        Enum.GetValues<PluginStatus>().Should().Contain(PluginStatus.Deleted);
    }

    [Fact]
    public void MediaType_HasAllExpectedValues()
    {
        Enum.GetValues<MediaType>().Should().HaveCount(5);
        Enum.GetValues<MediaType>().Should().Contain(MediaType.Movie);
        Enum.GetValues<MediaType>().Should().Contain(MediaType.TvShow);
        Enum.GetValues<MediaType>().Should().Contain(MediaType.Music);
        Enum.GetValues<MediaType>().Should().Contain(MediaType.Episode);
        Enum.GetValues<MediaType>().Should().Contain(MediaType.Season);
    }

    [Fact]
    public void EncodingProfile_HasDefaults()
    {
        EncodingProfile profile = new()
        {
            Name = "test",
            VideoCodec = "h264",
            AudioCodec = "aac"
        };

        profile.Container.Should().Be("mp4");
        profile.Width.Should().BeNull();
        profile.ExtraParameters.Should().BeEmpty();
    }

    [Fact]
    public void MediaMetadata_HasDefaults()
    {
        MediaMetadata metadata = new() { Title = "Test" };

        metadata.Overview.Should().BeNull();
        metadata.Year.Should().BeNull();
        metadata.Genres.Should().BeEmpty();
        metadata.Rating.Should().BeNull();
    }

    [Fact]
    public void AuthResult_HasDefaults()
    {
        AuthResult result = new() { IsAuthenticated = false };

        result.UserId.Should().BeNull();
        result.UserName.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.Claims.Should().BeEmpty();
    }

    [Fact]
    public void MediaFile_HasDefaults()
    {
        MediaFile file = new() { Path = "/test.mkv", FileName = "test.mkv" };

        file.Size.Should().Be(0);
        file.Type.Should().Be(MediaType.Movie);
        file.Properties.Should().BeEmpty();
    }

    [Fact]
    public void MediaInfo_HasDefaults()
    {
        MediaInfo info = new() { FilePath = "/test.mkv" };

        info.VideoCodec.Should().BeNull();
        info.AudioCodec.Should().BeNull();
        info.Width.Should().BeNull();
        info.Height.Should().BeNull();
        info.IsHdr.Should().BeFalse();
    }

    [Fact]
    public async Task Plugin_CanSubscribeToEventsViaContext()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(bus);

        List<IEvent> received = [];
        context.EventBus.Subscribe<NoMercy.Events.Playback.PlaybackStartedEvent>((evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new NoMercy.Events.Playback.PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "movie"
        });

        received.Should().ContainSingle();
    }
}
