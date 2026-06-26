using FluentAssertions;
using NoMercy.Encoder.Codecs;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;
using EncoderMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;
using EncoderProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using PluginMediaInfo = NoMercy.Plugins.Abstractions.MediaInfo;
using PluginProfile = NoMercy.Plugins.Abstractions.EncodingProfile;
using VideoStreamInfo = NoMercy.Encoder.Analysis.VideoStreamInfo;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Pins the plugin override wiring: IEncoderPlugin.GetProfile (the public plugin
/// surface, decided to OVERRIDE the configured profile when non-null) actually
/// drives the encode, bridged from the flat plugin DTO to the encoder's profile.
/// </summary>
public class PluginProfileOverrideTests
{
    [Fact]
    public void PluginReturnsProfile_OverridesConfigured()
    {
        PluginProfileOverride sut = new(
            new FakePluginManager(
                new FakePlugin(
                    new PluginProfile
                    {
                        Name = "Anime HEVC",
                        VideoCodec = "hevc",
                        AudioCodec = "aac",
                        Container = "hls",
                        Width = 1920,
                        Height = 1080,
                    }
                )
            )
        );

        EncoderProfile result = sut.Apply(Configured(), Source());

        result.Name.Should().Be("Anime HEVC", "the plugin's profile replaces the configured one");
        result.Video!.Codec.Should().Be(VideoCodecType.H265, "string codec mapped to the enum");
        result.Video.Width.Should().Be(1920);
    }

    [Fact]
    public void NoPlugin_KeepsConfigured()
    {
        PluginProfileOverride sut = new(new FakePluginManager());
        EncoderProfile configured = Configured();

        sut.Apply(configured, Source()).Should().BeSameAs(configured);
    }

    [Fact]
    public void FirstPluginReturningNonNull_Wins()
    {
        PluginProfileOverride sut = new(
            new FakePluginManager(
                new FakePlugin(null),
                new FakePlugin(NamedProfile("Winner")),
                new FakePlugin(NamedProfile("Loser"))
            )
        );

        sut.Apply(Configured(), Source()).Name.Should().Be("Winner");
    }

    private static PluginProfile NamedProfile(string name) =>
        new()
        {
            Name = name,
            VideoCodec = "h264",
            AudioCodec = "aac",
        };

    private static EncoderProfile Configured() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Configured Default",
            Container: Encoder.Profiles.Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: []
        );

    private static EncoderMediaInfo Source() =>
        new(
            FilePath: "/media/x.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 20000,
            FileSizeBytes: 10_000_000_000,
            VideoStreams:
            [
                new VideoStreamInfo(
                    Index: 0,
                    Codec: "hevc",
                    Width: 3840,
                    Height: 2160,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 18000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private sealed class FakePlugin(PluginProfile? result) : IEncoderPlugin
    {
        public string Name => "fake";
        public string Description => "";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(1, 0);

        public void Initialize(IPluginContext context) { }

        public void Dispose() { }

        public PluginProfile? GetProfile(PluginMediaInfo info) => result;
    }

    private sealed class FakePluginManager(params IEncoderPlugin[] plugins) : IPluginManager
    {
        public IEnumerable<T> GetPluginsOfType<T>()
            where T : IPlugin => plugins.OfType<T>();

        public IReadOnlyList<PluginInfo> GetInstalledPlugins() => [];

        public Task InstallPluginAsync(string packageUrl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PluginLoadResult>>([]);
    }
}
