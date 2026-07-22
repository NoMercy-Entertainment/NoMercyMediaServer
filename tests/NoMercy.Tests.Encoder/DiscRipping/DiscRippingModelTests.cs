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

        drive.Path.Should().Be(expected: "/dev/sr0");
        drive.Label.Should().Be(expected: "THE_MATRIX");
        drive.HasDisc.Should().BeTrue();
        drive.DiscType.Should().Be(expected: OpticalDiscType.BluRay);
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

        drive.DiscType.Should().Be(expected: OpticalDiscType.Dvd);
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

        drive.DiscType.Should().Be(expected: OpticalDiscType.Cd);
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
        drive.DiscType.Should().Be(expected: OpticalDiscType.None);
    }

    // ---------------------------------------------------------------------------
    // OpticalDiscType
    // ---------------------------------------------------------------------------

    [Fact]
    public void OpticalDiscType_HasAllFourValues()
    {
        string[] names = Enum.GetNames<OpticalDiscType>();

        names.Should().Contain(expected: "BluRay");
        names.Should().Contain(expected: "Dvd");
        names.Should().Contain(expected: "Cd");
        names.Should().Contain(expected: "None");
        names.Should().HaveCount(expected: 4);
    }

    // ---------------------------------------------------------------------------
    // DriveEvent
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(data: DriveEventType.DiscInserted)]
    [InlineData(data: DriveEventType.DiscEjected)]
    [InlineData(data: DriveEventType.DriveAdded)]
    [InlineData(data: DriveEventType.DriveRemoved)]
    public void DriveEvent_ConstructsCorrectly_ForEachEventType(DriveEventType eventType)
    {
        DiscDrive drive = new(
            Path: "/dev/sr0",
            Label: "TEST",
            HasDisc: true,
            DiscType: OpticalDiscType.BluRay
        );

        DriveEvent driveEvent = new(Type: eventType, Drive: drive);

        driveEvent.Type.Should().Be(expected: eventType);
        driveEvent.Drive.Should().Be(expected: drive);
    }

    // ---------------------------------------------------------------------------
    // DiscInfo — Blu-ray with titles + chapters
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscInfo_BluRay_ConstructsWithTitlesAndChapters()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 20), Title: "Chapter 1"),
            new(Start: TimeSpan.FromMinutes(minutes: 20), End: TimeSpan.FromMinutes(minutes: 40), Title: "Chapter 2"),
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
            Duration: TimeSpan.FromMinutes(minutes: 136),
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
            TotalDuration: TimeSpan.FromMinutes(minutes: 136)
        );

        disc.Type.Should().Be(expected: OpticalDiscType.BluRay);
        disc.DiscLabel.Should().Be(expected: "THE_MATRIX");
        disc.Titles.Should().HaveCount(expected: 1);
        disc.Titles[0].IsMainFeature.Should().BeTrue();
        disc.Titles[0].Chapters.Should().HaveCount(expected: 2);
        disc.Titles[0].VideoStreams.Should().HaveCount(expected: 1);
        disc.Titles[0].AudioStreams.Should().HaveCount(expected: 1);
        disc.Titles[0].Subtitles.Should().HaveCount(expected: 1);
        disc.Titles[0].EstimatedSizeBytes.Should().Be(expected: 45_000_000_000L);
        disc.TotalDuration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 136));
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
                Duration: TimeSpan.FromSeconds(seconds: 169),
                SampleRate: 44100,
                Channels: 2
            ),
            new(
                Index: 1,
                Title: "On the Run",
                Artist: "Pink Floyd",
                Duration: TimeSpan.FromSeconds(seconds: 233),
                SampleRate: 44100,
                Channels: 2
            ),
        ];

        DiscInfo disc = new(
            Type: OpticalDiscType.Cd,
            DiscLabel: "DARK_SIDE_OF_THE_MOON",
            Titles: [],
            AudioTracks: tracks,
            TotalDuration: TimeSpan.FromMinutes(minutes: 43)
        );

        disc.Type.Should().Be(expected: OpticalDiscType.Cd);
        disc.AudioTracks.Should().HaveCount(expected: 2);
        disc.AudioTracks![0].Title.Should().Be(expected: "Breathe");
        disc.AudioTracks![0].Artist.Should().Be(expected: "Pink Floyd");
        disc.AudioTracks![0].SampleRate.Should().Be(expected: 44100);
        disc.AudioTracks![0].Channels.Should().Be(expected: 2);
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
            Duration: TimeSpan.FromMinutes(minutes: 2),
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
            Duration: TimeSpan.FromMinutes(minutes: 120),
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 30_000_000_000L,
            IsMainFeature: true
        );

        DiscTitle[] titles = [shortTitle, mainFeature];

        DiscTitle longest = titles.MaxBy(keySelector: t => t.Duration)!;

        longest.IsMainFeature.Should().BeTrue();
        longest.Duration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 120));
    }

    // ---------------------------------------------------------------------------
    // DiscCandidate
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscCandidate_ConfidenceRange_IsValid()
    {
        DiscCandidate candidate = new(
            Source: "tmdb",
            StableId: "603",
            Title: "The Matrix",
            Year: 1999,
            PosterUrl: "https://image.tmdb.org/t/p/w500/abc.jpg",
            BackdropUrl: null,
            Confidence: 0.95,
            Type: MediaType.Movie
        );

        candidate.Confidence.Should().BeGreaterThanOrEqualTo(expected: 0.0);
        candidate.Confidence.Should().BeLessThanOrEqualTo(expected: 1.0);
        candidate.Source.Should().Be(expected: "tmdb");
        candidate.Title.Should().Be(expected: "The Matrix");
        candidate.Year.Should().Be(expected: 1999);
        candidate.StableId.Should().Be(expected: "603");
        candidate.Type.Should().Be(expected: MediaType.Movie);
    }

    [Fact]
    public void DiscCandidate_TvShow_TypeCorrect()
    {
        DiscCandidate candidate = new(
            Source: "tvdb",
            StableId: "81189",
            Title: "Breaking Bad",
            Year: 2008,
            PosterUrl: null,
            BackdropUrl: null,
            Confidence: 0.88,
            Type: MediaType.TvShow
        );

        candidate.Type.Should().Be(expected: MediaType.TvShow);
        candidate.PosterUrl.Should().BeNull();
    }

    [Fact]
    public void DiscCandidate_Music_TypeCorrect()
    {
        DiscCandidate candidate = new(
            Source: "musicbrainz",
            StableId: "a14a5a9f-3c8b-4f5d-bc3e-bb9f4e1e2a8c",
            Title: "The Dark Side of the Moon",
            Year: 1973,
            PosterUrl: null,
            BackdropUrl: null,
            Confidence: 0.72,
            Type: MediaType.Music
        );

        candidate.Type.Should().Be(expected: MediaType.Music);
        candidate.Confidence.Should().BeGreaterThanOrEqualTo(expected: 0.0).And.BeLessThanOrEqualTo(expected: 1.0);
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
            new(StreamIndex: 2, Include: true, Policy: SubtitlePolicy.Extract),
        ];

        Ulid libraryId = Ulid.Parse(base32: "01HMZXX9P3VK7BKFMTQ2AHKGWY");
        Ulid folderId = Ulid.Parse(base32: "01HMZXX9P3VK7BKFMTQ2AHKGWZ");

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

        request.DrivePath.Should().Be(expected: "/dev/sr0");
        request.SelectedTitleIndices.Should().Equal(elements: [0, 1]);
        request.MetadataId.Should().Be(expected: "tmdb:603");
        request.Custom.Should().BeNull();
        request.LibraryId.Should().Be(expected: libraryId);
        request.FolderId.Should().Be(expected: folderId);
        request.EncodingProfileId.Should().Be(expected: "hd-streaming");
        request.AudioTracks.Should().HaveCount(expected: 2);
        request.Subtitles.Should().HaveCount(expected: 1);
        request.AudioTracks[0].Include.Should().BeTrue();
        request.AudioTracks[1].Include.Should().BeFalse();
        request.Subtitles[0].Policy.Should().Be(expected: SubtitlePolicy.Extract);
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
            LibraryId: Ulid.Parse(base32: "01HMZXX9P3VK7BKFMTQ2AHKGWY"),
            FolderId: Ulid.Parse(base32: "01HMZXX9P3VK7BKFMTQ2AHKGWZ"),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: []
        );

        request.MetadataId.Should().BeNull();
        request.Custom.Should().NotBeNull();
        request.Custom!.Title.Should().Be(expected: "My Home Movie");
        request.Custom.Year.Should().Be(expected: 2024);
        request.Custom.Type.Should().Be(expected: MediaType.Movie);
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

        included.StreamIndex.Should().Be(expected: 0);
        included.Include.Should().BeTrue();
        excluded.StreamIndex.Should().Be(expected: 1);
        excluded.Include.Should().BeFalse();
    }

    [Fact]
    public void SubtitleSelection_ConstructsCorrectly_AllModes()
    {
        SubtitleSelection extract = new(
            StreamIndex: 0,
            Include: true,
            Policy: SubtitlePolicy.Extract
        );
        SubtitleSelection burnIn = new(
            StreamIndex: 1,
            Include: true,
            Policy: SubtitlePolicy.BurnIn
        );
        SubtitleSelection passThrough = new(
            StreamIndex: 2,
            Include: false,
            Policy: SubtitlePolicy.Copy
        );

        extract.Policy.Should().Be(expected: SubtitlePolicy.Extract);
        burnIn.Policy.Should().Be(expected: SubtitlePolicy.BurnIn);
        passThrough.Policy.Should().Be(expected: SubtitlePolicy.Copy);
        passThrough.Include.Should().BeFalse();
    }
}
