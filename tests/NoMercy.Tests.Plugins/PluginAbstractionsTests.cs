// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Playback;
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
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);
        public bool Initialized { get; private set; }
        public IPluginContext? ReceivedContext { get; private set; }

        public void Initialize(IPluginContext context)
        {
            ReceivedContext = context;
            Initialized = true;
        }

        public void Dispose() { }
    }

    private sealed class TestMetadataPlugin : IMetadataPlugin
    {
        public string Name => "Test Metadata";
        public string Description => "Test metadata provider";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);

        public void Initialize(IPluginContext context) { }

        public Task<MediaMetadata?> GetMetadataAsync(
            string title,
            MediaType type,
            CancellationToken ct = default
        )
        {
            return Task.FromResult<MediaMetadata?>(
                result: new()
                {
                    Title = title,
                    Year = 2024,
                    Genres = ["Action"],
                }
            );
        }

        public void Dispose() { }
    }

    private sealed class TestMediaSourcePlugin : IMediaSourcePlugin
    {
        public string Name => "Test Source";
        public string Description => "Test media source";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);

        public void Initialize(IPluginContext context) { }

        public Task<IEnumerable<MediaFile>> ScanAsync(string path, CancellationToken ct = default)
        {
            return Task.FromResult<IEnumerable<MediaFile>>(result:
            [
                new()
                {
                    Path = $"{path}/movie.mkv",
                    FileName = "movie.mkv",
                    Size = 1024,
                    Type = MediaType.Movie,
                },
            ]);
        }

        public void Dispose() { }
    }

    private sealed class TestEncoderPlugin : IEncoderPlugin
    {
        public string Name => "Test Encoder";
        public string Description => "Test encoder";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);

        public void Initialize(IPluginContext context) { }

        public EncodingProfile GetProfile(MediaInfo info)
        {
            return new()
            {
                Name = "test-profile",
                VideoCodec = "libx264",
                AudioCodec = "aac",
            };
        }

        public void Dispose() { }
    }

    private sealed class TestScheduledPlugin : IScheduledTaskPlugin
    {
        public string Name => "Test Scheduled";
        public string Description => "Test scheduled task";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);
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
        public Version Version { get; } = new(major: 1, minor: 0, build: 0);

        public void Initialize(IPluginContext context) { }

        public Task<AuthResult> AuthenticateAsync(string token, CancellationToken ct = default)
        {
            return Task.FromResult(
                result: new AuthResult
                {
                    IsAuthenticated = token == "valid-token",
                    UserName = token == "valid-token" ? "testuser" : null,
                }
            );
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
        public HttpClient HttpClient { get; }

        public TestPluginContext(IEventBus eventBus, string dataFolder = "/tmp/plugin-test")
        {
            EventBus = eventBus;
            Services = new MinimalServiceProvider();
            Logger = NullLogger.Instance;
            DataFolderPath = dataFolder;
            Configuration = new NullPluginConfiguration();
            HttpClient = new(handler: new HttpClientHandler());
        }

        private sealed class MinimalServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        private sealed class NullPluginConfiguration : IPluginConfiguration
        {
            public T? GetConfiguration<T>()
                where T : class, new() => null;

            public Task<T?> GetConfigurationAsync<T>(CancellationToken ct = default)
                where T : class, new() => Task.FromResult<T?>(result: null);

            public void SaveConfiguration<T>(T configuration)
                where T : class { }

            public Task SaveConfigurationAsync<T>(T configuration, CancellationToken ct = default)
                where T : class => Task.CompletedTask;

            public bool HasConfiguration() => false;

            public void DeleteConfiguration() { }
        }
    }

    [Fact]
    public void IPlugin_CanBeImplemented_WithRequiredProperties()
    {
        using TestPlugin plugin = new();

        plugin.Name.Should().Be(expected: "Test Plugin");
        plugin.Description.Should().NotBeNullOrEmpty();
        plugin.Id.Should().NotBe(unexpected: Guid.Empty);
        plugin.Version.Should().Be(expected: new(major: 1, minor: 0, build: 0));
    }

    [Fact]
    public void IPlugin_Initialize_ReceivesPluginContext()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(eventBus: bus);

        using TestPlugin plugin = new();
        plugin.Initialize(context: context);

        plugin.Initialized.Should().BeTrue();
        plugin.ReceivedContext.Should().BeSameAs(expected: context);
    }

    [Fact]
    public void IPluginContext_ProvidesEventBus()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(eventBus: bus);

        context.EventBus.Should().BeSameAs(expected: bus);
    }

    [Fact]
    public void IPluginContext_ProvidesLogger()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(eventBus: bus);

        context.Logger.Should().NotBeNull();
    }

    [Fact]
    public void IPluginContext_ProvidesDataFolderPath()
    {
        InMemoryEventBus bus = new();
        TestPluginContext context = new(eventBus: bus, dataFolder: "/data/plugins/my-plugin");

        context.DataFolderPath.Should().Be(expected: "/data/plugins/my-plugin");
    }

    [Fact]
    public async Task IMetadataPlugin_GetMetadataAsync_ReturnsMetadata()
    {
        using TestMetadataPlugin plugin = new();

        MediaMetadata? result = await plugin.GetMetadataAsync(title: "Test Movie", type: MediaType.Movie);

        result.Should().NotBeNull();
        result!.Title.Should().Be(expected: "Test Movie");
        result.Year.Should().Be(expected: 2024);
        result.Genres.Should().Contain(expected: "Action");
    }

    [Fact]
    public async Task IMediaSourcePlugin_ScanAsync_ReturnsFiles()
    {
        using TestMediaSourcePlugin plugin = new();

        List<MediaFile> files = (await plugin.ScanAsync(path: "/movies")).ToList();

        files.Should().ContainSingle();
        files[index: 0].FileName.Should().Be(expected: "movie.mkv");
        files[index: 0].Type.Should().Be(expected: MediaType.Movie);
        files[index: 0].Size.Should().Be(expected: 1024);
    }

    [Fact]
    public void IEncoderPlugin_GetProfile_ReturnsEncodingProfile()
    {
        using TestEncoderPlugin plugin = new();

        MediaInfo info = new() { FilePath = "/test.mkv" };
        EncodingProfile profile = plugin.GetProfile(info: info);

        profile.Name.Should().Be(expected: "test-profile");
        profile.VideoCodec.Should().Be(expected: "libx264");
        profile.AudioCodec.Should().Be(expected: "aac");
    }

    [Fact]
    public async Task IScheduledTaskPlugin_ExecuteAsync_RunsTask()
    {
        using TestScheduledPlugin plugin = new();

        plugin.CronExpression.Should().Be(expected: "0 0 * * *");
        plugin.Executed.Should().BeFalse();

        await plugin.ExecuteAsync();

        plugin.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task IAuthPlugin_AuthenticateAsync_ValidToken_ReturnsAuthenticated()
    {
        using TestAuthPlugin plugin = new();

        AuthResult result = await plugin.AuthenticateAsync(token: "valid-token");

        result.IsAuthenticated.Should().BeTrue();
        result.UserName.Should().Be(expected: "testuser");
    }

    [Fact]
    public async Task IAuthPlugin_AuthenticateAsync_InvalidToken_ReturnsNotAuthenticated()
    {
        using TestAuthPlugin plugin = new();

        AuthResult result = await plugin.AuthenticateAsync(token: "bad-token");

        result.IsAuthenticated.Should().BeFalse();
        result.UserName.Should().BeNull();
    }

    [Fact]
    public async Task IEncoderPlugin_GetProfileAsync_DefaultImplementation_WrapsGetProfile()
    {
        // TestEncoderPlugin never overrides GetProfileAsync — calling it through
        // the interface must dispatch to IEncoderPlugin's default implementation,
        // which wraps the synchronous GetProfile in Task.FromResult.
        using TestEncoderPlugin plugin = new();
        IEncoderPlugin encoderPlugin = plugin;
        MediaInfo info = new() { FilePath = "/test.mkv" };

        EncodingProfile? profile = await encoderPlugin.GetProfileAsync(info: info);

        profile.Should().NotBeNull();
        profile!.Name.Should().Be(expected: "test-profile");
    }

    [Fact]
    public async Task IPluginManager_InstallPluginAsyncWithChecksum_DefaultImplementation_ForwardsToTwoArgOverload()
    {
        // MinimalPluginManager never overrides the 3-arg InstallPluginAsync — calling
        // it through the interface must dispatch to IPluginManager's default
        // implementation, which forwards to the 2-arg (no-checksum) overload.
        MinimalPluginManager manager = new();
        IPluginManager pluginManager = manager;

        await pluginManager.InstallPluginAsync(packageUrl: "https://example.com/plugin.zip", expectedChecksum: "deadbeef");

        manager
            .ReceivedPackageUrls.Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "https://example.com/plugin.zip");
    }

    private sealed class MinimalPluginManager : IPluginManager
    {
        public List<string> ReceivedPackageUrls { get; } = [];

        public IReadOnlyList<PluginInfo> GetInstalledPlugins() => [];

        public Task InstallPluginAsync(string packageUrl, CancellationToken ct = default)
        {
            ReceivedPackageUrls.Add(item: packageUrl);
            return Task.CompletedTask;
        }

        public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PluginLoadResult>>(result: []);

        public IEnumerable<T> GetPluginsOfType<T>()
            where T : IPlugin => [];
    }

    [Fact]
    public void PluginInfo_HoldsPluginMetadata()
    {
        PluginInfo info = new()
        {
            Id = Guid.NewGuid(),
            Name = "My Plugin",
            Description = "A useful plugin",
            Version = new(major: 2, minor: 1, build: 0),
            Status = PluginStatus.Active,
            Author = "Test Author",
        };

        info.Name.Should().Be(expected: "My Plugin");
        info.Status.Should().Be(expected: PluginStatus.Active);
        info.Version.Should().Be(expected: new(major: 2, minor: 1, build: 0));
        info.Author.Should().Be(expected: "Test Author");
    }

    [Fact]
    public void PluginStatus_HasAllExpectedValues()
    {
        Enum.GetValues<PluginStatus>().Should().HaveCount(expected: 4);
        Enum.GetValues<PluginStatus>().Should().Contain(expected: PluginStatus.Active);
        Enum.GetValues<PluginStatus>().Should().Contain(expected: PluginStatus.Disabled);
        Enum.GetValues<PluginStatus>().Should().Contain(expected: PluginStatus.Malfunctioned);
        Enum.GetValues<PluginStatus>().Should().Contain(expected: PluginStatus.Deleted);
    }

    [Fact]
    public void MediaType_HasAllExpectedValues()
    {
        Enum.GetValues<MediaType>().Should().HaveCount(expected: 5);
        Enum.GetValues<MediaType>().Should().Contain(expected: MediaType.Movie);
        Enum.GetValues<MediaType>().Should().Contain(expected: MediaType.TvShow);
        Enum.GetValues<MediaType>().Should().Contain(expected: MediaType.Music);
        Enum.GetValues<MediaType>().Should().Contain(expected: MediaType.Episode);
        Enum.GetValues<MediaType>().Should().Contain(expected: MediaType.Season);
    }

    [Fact]
    public void EncodingProfile_HasDefaults()
    {
        EncodingProfile profile = new()
        {
            Name = "test",
            VideoCodec = "h264",
            AudioCodec = "aac",
        };

        profile.Container.Should().Be(expected: "mp4");
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

        file.Size.Should().Be(expected: 0);
        file.Type.Should().Be(expected: MediaType.Movie);
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
        TestPluginContext context = new(eventBus: bus);

        List<IEvent> received = [];
        context.EventBus.Subscribe<PlaybackStartedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        received.Should().ContainSingle();
    }
}
