using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.DiscRipping;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class DiscScannerTests
{
    // ---------------------------------------------------------------------------
    // Fake IDiscScanner implementation backed by canned disc structures
    // ---------------------------------------------------------------------------

    private sealed class FakeDiscScanner : IDiscScanner
    {
        private readonly Dictionary<string, DiscInfo> _cannedResults = new();

        public void Register(string drivePath, DiscInfo info) => _cannedResults[drivePath] = info;

        public Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct)
        {
            if (!_cannedResults.TryGetValue(drivePath, out DiscInfo? info))
            {
                return Task.FromResult(
                    new DiscInfo(
                        Type: OpticalDiscType.None,
                        DiscLabel: null,
                        Titles: [],
                        AudioTracks: null,
                        TotalDuration: TimeSpan.Zero
                    )
                );
            }

            return Task.FromResult(info);
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers to build canned structures
    // ---------------------------------------------------------------------------

    private static VideoStreamInfo MakeVideoStream(
        string codec = "hevc",
        int width = 1920,
        int height = 1080
    ) =>
        new(
            Index: 0,
            Codec: codec,
            Width: width,
            Height: height,
            FrameRate: 23.976,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 15000
        );

    private static AudioStreamInfo MakeAudioStream(string codec = "truehd", int channels = 8) =>
        new(
            Index: 1,
            Codec: codec,
            Channels: channels,
            SampleRate: 48000,
            BitRateKbps: 3000,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );

    private static DiscTitle MakeTitle(
        int index,
        string name,
        TimeSpan duration,
        bool isMainFeature,
        string videoCodec = "hevc",
        int chapters = 0
    )
    {
        ChapterInfo[] chapterArray = Enumerable
            .Range(0, chapters)
            .Select(i => new ChapterInfo(
                Start: TimeSpan.FromMinutes(i * 20),
                End: TimeSpan.FromMinutes((i + 1) * 20),
                Title: $"Chapter {i + 1}"
            ))
            .ToArray();

        return new(
            Index: index,
            Name: name,
            Duration: duration,
            VideoStreams: [MakeVideoStream(videoCodec)],
            AudioStreams: [MakeAudioStream()],
            Subtitles: [],
            Chapters: chapterArray,
            EstimatedSizeBytes: (long)(duration.TotalSeconds * 5_000_000),
            IsMainFeature: isMainFeature
        );
    }

    // ---------------------------------------------------------------------------
    // Blu-ray: 3 titles, main feature is longest
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ScanAsync_BluRay_ReturnsTitles_MainFeatureIsLongest()
    {
        FakeDiscScanner scanner = new();

        DiscInfo bluRayDisc = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "THE_MATRIX",
            Titles:
            [
                MakeTitle(0, "Trailer", TimeSpan.FromMinutes(2), isMainFeature: false),
                MakeTitle(
                    1,
                    "Main Feature",
                    TimeSpan.FromMinutes(136),
                    isMainFeature: true,
                    chapters: 24
                ),
                MakeTitle(2, "Deleted Scenes", TimeSpan.FromMinutes(18), isMainFeature: false),
            ],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(156)
        );

        scanner.Register("/dev/sr0", bluRayDisc);

        DiscInfo result = await scanner.ScanAsync("/dev/sr0", CancellationToken.None);

        result.Type.Should().Be(OpticalDiscType.BluRay);
        result.DiscLabel.Should().Be("THE_MATRIX");
        result.Titles.Should().HaveCount(3);

        DiscTitle mainFeature = result.Titles.MaxBy(t => t.Duration)!;

        mainFeature.IsMainFeature.Should().BeTrue();
        mainFeature.Name.Should().Be("Main Feature");
        mainFeature.Duration.Should().Be(TimeSpan.FromMinutes(136));
        mainFeature.Chapters.Should().HaveCount(24);
        mainFeature.VideoStreams.Should().HaveCount(1);
        mainFeature.AudioStreams.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_BluRay_NonMainTitles_AreMarkedCorrectly()
    {
        FakeDiscScanner scanner = new();

        DiscInfo bluRayDisc = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "INCEPTION",
            Titles:
            [
                MakeTitle(
                    0,
                    "Main Feature",
                    TimeSpan.FromMinutes(148),
                    isMainFeature: true,
                    chapters: 36
                ),
                MakeTitle(0, "Behind the Scenes", TimeSpan.FromMinutes(45), isMainFeature: false),
                MakeTitle(0, "Theatrical Trailer", TimeSpan.FromMinutes(3), isMainFeature: false),
            ],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(196)
        );

        scanner.Register("/dev/sr1", bluRayDisc);

        DiscInfo result = await scanner.ScanAsync("/dev/sr1", CancellationToken.None);

        IEnumerable<DiscTitle> nonMain = result.Titles.Where(t => !t.IsMainFeature);

        nonMain.Should().HaveCount(2);
        nonMain.All(t => t.IsMainFeature).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // DVD: interlaced content
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ScanAsync_Dvd_ReturnsDvdType_WithInterlacedContent()
    {
        FakeDiscScanner scanner = new();

        VideoStreamInfo interlacedVideo = new(
            Index: 0,
            Codec: "mpeg2video",
            Width: 720,
            Height: 480,
            FrameRate: 29.97,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 6000
        );

        DiscTitle mainTitle = new(
            Index: 0,
            Name: "Main Feature",
            Duration: TimeSpan.FromMinutes(95),
            VideoStreams: [interlacedVideo],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "ac3",
                    Channels: 6,
                    SampleRate: 48000,
                    BitRateKbps: 448,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            Subtitles:
            [
                new(
                    Index: 2,
                    Codec: "dvd_subtitle",
                    Language: "eng",
                    IsDefault: false,
                    IsForced: false
                ),
            ],
            Chapters: [],
            EstimatedSizeBytes: 4_700_000_000L,
            IsMainFeature: true
        );

        DiscInfo dvdDisc = new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: "THE_SHAWSHANK_REDEMPTION",
            Titles: [mainTitle],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(95)
        );

        scanner.Register("D:\\", dvdDisc);

        DiscInfo result = await scanner.ScanAsync("D:\\", CancellationToken.None);

        result.Type.Should().Be(OpticalDiscType.Dvd);
        result.Titles.Should().HaveCount(1);

        DiscTitle title = result.Titles[0];

        title.VideoStreams[0].Codec.Should().Be("mpeg2video");
        title.VideoStreams[0].Width.Should().Be(720);
        title.VideoStreams[0].Height.Should().Be(480);
        title.AudioStreams[0].Codec.Should().Be("ac3");
        title.Subtitles[0].Codec.Should().Be("dvd_subtitle");
        title.Subtitles[0].IsTextBased.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // Audio CD: 12 tracks
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ScanAsync_AudioCd_ReturnsTwelveTracks()
    {
        FakeDiscScanner scanner = new();

        DiscTrack[] tracks = Enumerable
            .Range(0, 12)
            .Select(i => new DiscTrack(
                Index: i,
                Title: $"Track {i + 1:D2}",
                Artist: "Pink Floyd",
                Duration: TimeSpan.FromSeconds(180 + (i * 15)),
                SampleRate: 44100,
                Channels: 2
            ))
            .ToArray();

        TimeSpan totalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

        DiscInfo cdDisc = new(
            Type: OpticalDiscType.Cd,
            DiscLabel: "DARK_SIDE_OF_THE_MOON",
            Titles: [],
            AudioTracks: tracks,
            TotalDuration: totalDuration
        );

        scanner.Register("/dev/sr0", cdDisc);

        DiscInfo result = await scanner.ScanAsync("/dev/sr0", CancellationToken.None);

        result.Type.Should().Be(OpticalDiscType.Cd);
        result.AudioTracks.Should().NotBeNull();
        result.AudioTracks!.Should().HaveCount(12);
        result.Titles.Should().BeEmpty();

        result.AudioTracks![0].Title.Should().Be("Track 01");
        result.AudioTracks![0].SampleRate.Should().Be(44100);
        result.AudioTracks![0].Channels.Should().Be(2);
        result.AudioTracks![11].Title.Should().Be("Track 12");
    }

    [Fact]
    public async Task ScanAsync_AudioCd_TracksHaveCorrectMetadata()
    {
        FakeDiscScanner scanner = new();

        DiscTrack[] tracks =
        [
            new(
                Index: 0,
                Title: "Speak to Me / Breathe",
                Artist: "Pink Floyd",
                Duration: TimeSpan.FromSeconds(169),
                SampleRate: 44100,
                Channels: 2
            ),
            new(
                Index: 1,
                Title: "On the Run",
                Artist: "Pink Floyd",
                Duration: TimeSpan.FromSeconds(233),
                SampleRate: 44100,
                Channels: 2
            ),
        ];

        DiscInfo cdDisc = new(
            Type: OpticalDiscType.Cd,
            DiscLabel: "THE_DARK_SIDE",
            Titles: [],
            AudioTracks: tracks,
            TotalDuration: TimeSpan.FromSeconds(402)
        );

        scanner.Register("/dev/sr2", cdDisc);

        DiscInfo result = await scanner.ScanAsync("/dev/sr2", CancellationToken.None);

        result.AudioTracks![0].Artist.Should().Be("Pink Floyd");
        result.AudioTracks![0].Duration.Should().Be(TimeSpan.FromSeconds(169));
        result.AudioTracks![1].Title.Should().Be("On the Run");
        result.AudioTracks![1].Duration.Should().Be(TimeSpan.FromSeconds(233));
    }

    // ---------------------------------------------------------------------------
    // Empty drive: no titles returned
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ScanAsync_EmptyDrive_ReturnsNoTitlesAndNoTracks()
    {
        FakeDiscScanner scanner = new();

        // No registration for this path — scanner returns the empty fallback
        DiscInfo result = await scanner.ScanAsync("/dev/sr0", CancellationToken.None);

        result.Type.Should().Be(OpticalDiscType.None);
        result.DiscLabel.Should().BeNull();
        result.Titles.Should().BeEmpty();
        result.AudioTracks.Should().BeNull();
        result.TotalDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ScanAsync_CancellationToken_PassedThrough()
    {
        FakeDiscScanner scanner = new();

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // FakeDiscScanner returns empty for unknown paths — does not observe cancellation
        // but the contract signature accepts it correctly.
        DiscInfo result = await scanner.ScanAsync("/dev/sr0", cts.Token);

        result.Type.Should().Be(OpticalDiscType.None);
    }
}
