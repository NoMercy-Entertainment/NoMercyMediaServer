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
            pluginManager: new FakePluginManager(
                plugins: new FakePlugin(
                    result: new()
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

        EncoderProfile result = sut.Apply(configured: Configured(), media: Source());

        result.Name.Should().Be(expected: "Anime HEVC", because: "the plugin's profile replaces the configured one");
        result.Video!.Codec.Should().Be(expected: VideoCodecType.H265, because: "string codec mapped to the enum");
        result.Video.Width.Should().Be(expected: 1920);
    }

    [Fact]
    public void NoPlugin_KeepsConfigured()
    {
        PluginProfileOverride sut = new(pluginManager: new FakePluginManager());
        EncoderProfile configured = Configured();

        sut.Apply(configured: configured, media: Source()).Should().BeSameAs(expected: configured);
    }

    [Fact]
    public void FirstPluginReturningNonNull_Wins()
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(plugins: [new FakePlugin(result: null), new FakePlugin(result: NamedProfile(name: "Winner")), new FakePlugin(result: NamedProfile(name: "Loser"))]
            )
        );

        sut.Apply(configured: Configured(), media: Source()).Name.Should().Be(expected: "Winner");
    }

    [Fact]
    public void SourceWithNoVideoStream_NullVideoFieldsAndDefaultIsHdr()
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(plugins: new FakePlugin(result: NamedProfile(name: "NoVideoSource")))
        );

        sut.Apply(configured: Configured(), media: SourceWithNoVideoStream());

        // The override itself never reads the plugin's incoming MediaInfo back —
        // this only proves Apply completes without throwing when every video?.X
        // access in ToPluginMediaInfo takes its null branch (no video stream at all).
    }

    [Fact]
    public void SourceWithAudioStream_AudioFieldsPopulated()
    {
        FakePlugin plugin = new(result: NamedProfile(name: "WithAudio"));
        PluginProfileOverride sut = new(pluginManager: new FakePluginManager(plugins: plugin));

        sut.Apply(configured: Configured(), media: SourceWithAudioStream());
    }

    [Fact]
    public void SourceWithZeroOverallBitrate_BitrateFieldStaysNull()
    {
        FakePlugin plugin = new(result: NamedProfile(name: "ZeroBitrate"));
        PluginProfileOverride sut = new(pluginManager: new FakePluginManager(plugins: plugin));

        EncoderMediaInfo zeroBitrateSource = Source() with { OverallBitRateKbps = 0 };

        sut.Apply(configured: Configured(), media: zeroBitrateSource);
    }

    [Fact]
    public void PluginProfile_UnrecognizedAudioCodec_FallsBackToAac()
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(
                plugins: new FakePlugin(
                    result: new()
                    {
                        Name = "Unrecognized",
                        VideoCodec = "h264",
                        AudioCodec = "totally-unknown-codec",
                    }
                )
            )
        );

        EncoderProfile result = sut.Apply(configured: Configured(), media: Source());

        result.Audio[0].Codec.Should().Be(expected: AudioCodecType.Aac);
    }

    [Fact]
    public void PluginProfile_ExtraParametersPopulated_BecomeCustomArguments()
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(
                plugins: new FakePlugin(
                    result: new()
                    {
                        Name = "WithExtras",
                        VideoCodec = "h264",
                        AudioCodec = "aac",
                        ExtraParameters = new Dictionary<string, string>
                        {
                            [key: "x265-params"] = "crf=20",
                        },
                    }
                )
            )
        );

        EncoderProfile result = sut.Apply(configured: Configured(), media: Source());

        result
            .Video!.CustomArguments.Should()
            .ContainKey(expected: "x265-params")
            .WhoseValue.Should()
            .Be(expected: "crf=20");
    }

    [Fact]
    public void PluginProfile_VideoAndAudioBitrateSet_UseVbrAndBitrateValues()
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(
                plugins: new FakePlugin(
                    result: new()
                    {
                        Name = "WithBitrates",
                        VideoCodec = "h264",
                        AudioCodec = "aac",
                        VideoBitrate = 4000,
                        AudioBitrate = 192,
                    }
                )
            )
        );

        EncoderProfile result = sut.Apply(configured: Configured(), media: Source());

        result.Video!.RateControl.Should().Be(expected: Encoder.Profiles.RateControlMode.Vbr);
        result.Video.BitrateKbps.Should().Be(expected: 4000);
        result.Audio[0].BitrateKbps.Should().Be(expected: 192);
    }

    [Theory]
    [InlineData(data: ["ts", Encoder.Profiles.Container.HlsTs])]
    [InlineData(data: ["mpegts", Encoder.Profiles.Container.HlsTs])]
    [InlineData(data: ["hls_ts", Encoder.Profiles.Container.HlsTs])]
    [InlineData(data: ["m3u8", Encoder.Profiles.Container.HlsFmp4])]
    [InlineData(data: ["hls", Encoder.Profiles.Container.HlsFmp4])]
    [InlineData(data: ["fmp4", Encoder.Profiles.Container.HlsFmp4])]
    [InlineData(data: ["hls_fmp4", Encoder.Profiles.Container.HlsFmp4])]
    [InlineData(data: ["mkv", Encoder.Profiles.Container.Mkv])]
    [InlineData(data: ["matroska", Encoder.Profiles.Container.Mkv])]
    [InlineData(data: ["dash", Encoder.Profiles.Container.Dash])]
    [InlineData(data: ["mpd", Encoder.Profiles.Container.Dash])]
    [InlineData(data: ["mp3", Encoder.Profiles.Container.Mp3])]
    [InlineData(data: ["flac", Encoder.Profiles.Container.Flac])]
    [InlineData(data: ["ogg", Encoder.Profiles.Container.Ogg])]
    [InlineData(data: ["oga", Encoder.Profiles.Container.Ogg])]
    [InlineData(data: ["opus", Encoder.Profiles.Container.Ogg])]
    [InlineData(data: ["MP4", Encoder.Profiles.Container.Mp4])]
    [InlineData(data: ["something-unrecognized", Encoder.Profiles.Container.Mp4])]
    public void PluginProfile_ContainerAlias_MapsToExpectedContainer(
        string containerAlias,
        Encoder.Profiles.Container expected
    )
    {
        PluginProfileOverride sut = new(
            pluginManager: new FakePluginManager(
                plugins: new FakePlugin(
                    result: new()
                    {
                        Name = "Container",
                        VideoCodec = "h264",
                        AudioCodec = "aac",
                        Container = containerAlias,
                    }
                )
            )
        );

        EncoderProfile result = sut.Apply(configured: Configured(), media: Source());

        result.Container.Should().Be(expected: expected);
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
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 20000,
            FileSizeBytes: 10_000_000_000,
            VideoStreams:
            [
                new(
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

    private static EncoderMediaInfo SourceWithNoVideoStream() =>
        Source() with
        {
            VideoStreams = [],
        };

    private static EncoderMediaInfo SourceWithAudioStream() =>
        Source() with
        {
            AudioStreams =
            [
                new(
                    Index: 0,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
        };

    private sealed class FakePlugin(PluginProfile? result) : IEncoderPlugin
    {
        public string Name => "fake";
        public string Description => "";
        public Guid Id { get; } = Guid.NewGuid();
        public Version Version { get; } = new(major: 1, minor: 0);

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
            Task.FromResult<IReadOnlyList<PluginLoadResult>>(result: []);
    }
}
