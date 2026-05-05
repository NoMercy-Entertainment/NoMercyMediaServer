using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class DiscRippingModelTests
{
    // ---------------------------------------------------------------------------
    // DiscDrive
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscDrive_BluRay_ConstructsCorrectly()
    {
        DiscDrive drive = new(
            Path: "/dev/sr0",
            Label: "THE_MATRIX",
            HasDisc: true,
            DiscType: OpticalDiscType.BluRay
        );

        drive.Path.Should().Be("/dev/sr0");
        drive.Label.Should().Be("THE_MATRIX");
        drive.HasDisc.Should().BeTrue();
        drive.DiscType.Should().Be(OpticalDiscType.BluRay);
    }

    [Fact]
    public void DiscDrive_Dvd_ConstructsCorrectly()
    {
        DiscDrive drive = new(
            Path: "D:\\",
            Label: "INCEPTION_D1",
            HasDisc: true,
            DiscType: OpticalDiscType.Dvd
        );

        drive.DiscType.Should().Be(OpticalDiscType.Dvd);
        drive.HasDisc.Should().BeTrue();
    }

    [Fact]
    public void DiscDrive_Cd_ConstructsCorrectly()
    {
        DiscDrive drive = new(
            Path: "/dev/sr1",
            Label: "DARK_SIDE",
            HasDisc: true,
            DiscType: OpticalDiscType.Cd
        );

        drive.DiscType.Should().Be(OpticalDiscType.Cd);
    }

    [Fact]
    public void DiscDrive_EmptyDrive_HasDiscFalse()
    {
        DiscDrive drive = new(
            Path: "/dev/sr0",
            Label: "",
            HasDisc: false,
            DiscType: OpticalDiscType.None
        );

        drive.HasDisc.Should().BeFalse();
        drive.DiscType.Should().Be(OpticalDiscType.None);
    }

    // ---------------------------------------------------------------------------
    // OpticalDiscType
    // ---------------------------------------------------------------------------

    [Fact]
    public void OpticalDiscType_HasAllFourValues()
    {
        string[] names = Enum.GetNames<OpticalDiscType>();

        names.Should().Contain("BluRay");
        names.Should().Contain("Dvd");
        names.Should().Contain("Cd");
        names.Should().Contain("None");
        names.Should().HaveCount(4);
    }

    // ---------------------------------------------------------------------------
    // DriveEvent
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(DriveEventType.DiscInserted)]
    [InlineData(DriveEventType.DiscEjected)]
    [InlineData(DriveEventType.DriveAdded)]
    [InlineData(DriveEventType.DriveRemoved)]
    public void DriveEvent_ConstructsCorrectly_ForEachEventType(DriveEventType eventType)
    {
        DiscDrive drive = new(
            Path: "/dev/sr0",
            Label: "TEST",
            HasDisc: true,
            DiscType: OpticalDiscType.BluRay
        );

        DriveEvent driveEvent = new(Type: eventType, Drive: drive);

        driveEvent.Type.Should().Be(eventType);
        driveEvent.Drive.Should().Be(drive);
    }

    // ---------------------------------------------------------------------------
    // DiscInfo — Blu-ray with titles + chapters
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscInfo_BluRay_ConstructsWithTitlesAndChapters()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(20), Title: "Chapter 1"),
            new(Start: TimeSpan.FromMinutes(20), End: TimeSpan.FromMinutes(40), Title: "Chapter 2"),
        ];

        VideoStreamInfo[] videoStreams =
        [
            new(
                Index: 0,
                Codec: "hevc",
                Width: 1920,
                Height: 1080,
                FrameRate: 23.976,
                BitDepth: 10,
                PixelFormat: "yuv420p10le",
                ColorPrimaries: "bt2020",
                ColorTransfer: "smpte2084",
                ColorSpace: "bt2020nc",
                IsDefault: true,
                BitRateKbps: 25000
            ),
        ];

        AudioStreamInfo[] audioStreams =
        [
            new(
                Index: 1,
                Codec: "truehd",
                Channels: 8,
                SampleRate: 48000,
                BitRateKbps: 3000,
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        SubtitleStreamInfo[] subtitles =
        [
            new(
                Index: 2,
                Codec: "hdmv_pgs_subtitle",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        DiscTitle title = new(
            Index: 0,
            Name: "The Matrix",
            Duration: TimeSpan.FromMinutes(136),
            VideoStreams: videoStreams,
            AudioStreams: audioStreams,
            Subtitles: subtitles,
            Chapters: chapters,
            EstimatedSizeBytes: 45_000_000_000L,
            IsMainFeature: true
        );

        DiscInfo disc = new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "THE_MATRIX",
            Titles: [title],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(136)
        );

        disc.Type.Should().Be(OpticalDiscType.BluRay);
        disc.DiscLabel.Should().Be("THE_MATRIX");
        disc.Titles.Should().HaveCount(1);
        disc.Titles[0].IsMainFeature.Should().BeTrue();
        disc.Titles[0].Chapters.Should().HaveCount(2);
        disc.Titles[0].VideoStreams.Should().HaveCount(1);
        disc.Titles[0].AudioStreams.Should().HaveCount(1);
        disc.Titles[0].Subtitles.Should().HaveCount(1);
        disc.Titles[0].EstimatedSizeBytes.Should().Be(45_000_000_000L);
        disc.TotalDuration.Should().Be(TimeSpan.FromMinutes(136));
        disc.AudioTracks.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // DiscInfo — Audio CD with tracks
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscInfo_AudioCd_ConstructsWithTracks()
    {
        DiscTrack[] tracks =
        [
            new(
                Index: 0,
                Title: "Breathe",
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

        DiscInfo disc = new(
            Type: OpticalDiscType.Cd,
            DiscLabel: "DARK_SIDE_OF_THE_MOON",
            Titles: [],
            AudioTracks: tracks,
            TotalDuration: TimeSpan.FromMinutes(43)
        );

        disc.Type.Should().Be(OpticalDiscType.Cd);
        disc.AudioTracks.Should().HaveCount(2);
        disc.AudioTracks![0].Title.Should().Be("Breathe");
        disc.AudioTracks![0].Artist.Should().Be("Pink Floyd");
        disc.AudioTracks![0].SampleRate.Should().Be(44100);
        disc.AudioTracks![0].Channels.Should().Be(2);
        disc.Titles.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // DiscTitle — IsMainFeature (longest title)
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscTitle_LongestTitle_CanBeIdentifiedAsMainFeature()
    {
        DiscTitle shortTitle = new(
            Index: 0,
            Name: "Trailer",
            Duration: TimeSpan.FromMinutes(2),
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 200_000_000L,
            IsMainFeature: false
        );

        DiscTitle mainFeature = new(
            Index: 1,
            Name: "Main Feature",
            Duration: TimeSpan.FromMinutes(120),
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 30_000_000_000L,
            IsMainFeature: true
        );

        DiscTitle[] titles = [shortTitle, mainFeature];

        DiscTitle longest = titles.MaxBy(t => t.Duration)!;

        longest.IsMainFeature.Should().BeTrue();
        longest.Duration.Should().Be(TimeSpan.FromMinutes(120));
    }

    // ---------------------------------------------------------------------------
    // MetadataMatch
    // ---------------------------------------------------------------------------

    [Fact]
    public void MetadataMatch_ConfidenceRange_IsValid()
    {
        MetadataMatch match = new(
            Source: "tmdb",
            Confidence: 0.95,
            Title: "The Matrix",
            Year: 1999,
            PosterUrl: "https://image.tmdb.org/t/p/w500/abc.jpg",
            ExternalId: "603",
            Type: MediaType.Movie
        );

        match.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
        match.Confidence.Should().BeLessThanOrEqualTo(1.0);
        match.Source.Should().Be("tmdb");
        match.Title.Should().Be("The Matrix");
        match.Year.Should().Be(1999);
        match.ExternalId.Should().Be("603");
        match.Type.Should().Be(MediaType.Movie);
    }

    [Fact]
    public void MetadataMatch_TvShow_TypeCorrect()
    {
        MetadataMatch match = new(
            Source: "tvdb",
            Confidence: 0.88,
            Title: "Breaking Bad",
            Year: 2008,
            PosterUrl: null,
            ExternalId: "81189",
            Type: MediaType.TvShow
        );

        match.Type.Should().Be(MediaType.TvShow);
        match.PosterUrl.Should().BeNull();
    }

    [Fact]
    public void MetadataMatch_Music_TypeCorrect()
    {
        MetadataMatch match = new(
            Source: "musicbrainz",
            Confidence: 0.72,
            Title: "The Dark Side of the Moon",
            Year: 1973,
            PosterUrl: null,
            ExternalId: "a14a5a9f-3c8b-4f5d-bc3e-bb9f4e1e2a8c",
            Type: MediaType.Music
        );

        match.Type.Should().Be(MediaType.Music);
        match.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    // ---------------------------------------------------------------------------
    // RipRequest
    // ---------------------------------------------------------------------------

    [Fact]
    public void RipRequest_WithSelectedTitlesAndAudioTracks_ConstructsCorrectly()
    {
        AudioTrackSelection[] audioTracks =
        [
            new(StreamIndex: 0, Include: true),
            new(StreamIndex: 1, Include: false),
        ];

        SubtitleSelection[] subtitles =
        [
            new(StreamIndex: 2, Include: true, Mode: SubtitleMode.Extract),
        ];

        Ulid libraryId = Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWY");
        Ulid folderId = Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWZ");

        RipRequest request = new(
            DrivePath: "/dev/sr0",
            SelectedTitleIndices: [0, 1],
            MetadataId: "tmdb:603",
            Custom: null,
            LibraryId: libraryId,
            FolderId: folderId,
            EncodingProfileId: "hd-streaming",
            AudioTracks: audioTracks,
            Subtitles: subtitles
        );

        request.DrivePath.Should().Be("/dev/sr0");
        request.SelectedTitleIndices.Should().Equal(0, 1);
        request.MetadataId.Should().Be("tmdb:603");
        request.Custom.Should().BeNull();
        request.LibraryId.Should().Be(libraryId);
        request.FolderId.Should().Be(folderId);
        request.EncodingProfileId.Should().Be("hd-streaming");
        request.AudioTracks.Should().HaveCount(2);
        request.Subtitles.Should().HaveCount(1);
        request.AudioTracks[0].Include.Should().BeTrue();
        request.AudioTracks[1].Include.Should().BeFalse();
        request.Subtitles[0].Mode.Should().Be(SubtitleMode.Extract);
    }

    [Fact]
    public void RipRequest_WithCustomMetadataFallback_ConstructsCorrectly()
    {
        CustomMetadata custom = new(
            Title: "My Home Movie",
            Year: 2024,
            Type: MediaType.Movie,
            PosterUrl: null
        );

        RipRequest request = new(
            DrivePath: "E:\\",
            SelectedTitleIndices: [0],
            MetadataId: null,
            Custom: custom,
            LibraryId: Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWY"),
            FolderId: Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWZ"),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: []
        );

        request.MetadataId.Should().BeNull();
        request.Custom.Should().NotBeNull();
        request.Custom!.Title.Should().Be("My Home Movie");
        request.Custom.Year.Should().Be(2024);
        request.Custom.Type.Should().Be(MediaType.Movie);
        request.Custom.PosterUrl.Should().BeNull();
        request.EncodingProfileId.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // AudioTrackSelection and SubtitleSelection
    // ---------------------------------------------------------------------------

    [Fact]
    public void AudioTrackSelection_ConstructsCorrectly()
    {
        AudioTrackSelection included = new(StreamIndex: 0, Include: true);
        AudioTrackSelection excluded = new(StreamIndex: 1, Include: false);

        included.StreamIndex.Should().Be(0);
        included.Include.Should().BeTrue();
        excluded.StreamIndex.Should().Be(1);
        excluded.Include.Should().BeFalse();
    }

    [Fact]
    public void SubtitleSelection_ConstructsCorrectly_AllModes()
    {
        SubtitleSelection extract = new(StreamIndex: 0, Include: true, Mode: SubtitleMode.Extract);
        SubtitleSelection burnIn = new(StreamIndex: 1, Include: true, Mode: SubtitleMode.BurnIn);
        SubtitleSelection passThrough = new(
            StreamIndex: 2,
            Include: false,
            Mode: SubtitleMode.PassThrough
        );

        extract.Mode.Should().Be(SubtitleMode.Extract);
        burnIn.Mode.Should().Be(SubtitleMode.BurnIn);
        passThrough.Mode.Should().Be(SubtitleMode.PassThrough);
        passThrough.Include.Should().BeFalse();
    }
}
