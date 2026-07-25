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
            "/dev/sr0",
            "THE_MATRIX",
            true,
            OpticalDiscType.BluRay
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
            "D:\\",
            "INCEPTION_D1",
            true,
            OpticalDiscType.Dvd
        );

        drive.DiscType.Should().Be(OpticalDiscType.Dvd);
        drive.HasDisc.Should().BeTrue();
    }

    [Fact]
    public void DiscDrive_Cd_ConstructsCorrectly()
    {
        DiscDrive drive = new(
            "/dev/sr1",
            "DARK_SIDE",
            true,
            OpticalDiscType.Cd
        );

        drive.DiscType.Should().Be(OpticalDiscType.Cd);
    }

    [Fact]
    public void DiscDrive_EmptyDrive_HasDiscFalse()
    {
        DiscDrive drive = new(
            "/dev/sr0",
            "",
            false,
            OpticalDiscType.None
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
            "/dev/sr0",
            "TEST",
            true,
            OpticalDiscType.BluRay
        );

        DriveEvent driveEvent = new(eventType, drive);

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
            new(TimeSpan.Zero, TimeSpan.FromMinutes(20), "Chapter 1"),
            new(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(40), "Chapter 2"),
        ];

        VideoStreamInfo[] videoStreams =
        [
            new(
                0,
                "hevc",
                1920,
                1080,
                23.976,
                10,
                "yuv420p10le",
                "bt2020",
                "smpte2084",
                "bt2020nc",
                true,
                25000
            ),
        ];

        AudioStreamInfo[] audioStreams =
        [
            new(
                1,
                "truehd",
                8,
                48000,
                3000,
                "eng",
                true,
                false
            ),
        ];

        SubtitleStreamInfo[] subtitles =
        [
            new(
                2,
                "hdmv_pgs_subtitle",
                "eng",
                true,
                false
            ),
        ];

        DiscTitle title = new(
            0,
            "The Matrix",
            TimeSpan.FromMinutes(136),
            videoStreams,
            audioStreams,
            subtitles,
            chapters,
            45_000_000_000L,
            true
        );

        DiscInfo disc = new(
            OpticalDiscType.BluRay,
            "THE_MATRIX",
            [title],
            null,
            TimeSpan.FromMinutes(136)
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
                0,
                "Breathe",
                "Pink Floyd",
                TimeSpan.FromSeconds(169),
                44100,
                2
            ),
            new(
                1,
                "On the Run",
                "Pink Floyd",
                TimeSpan.FromSeconds(233),
                44100,
                2
            ),
        ];

        DiscInfo disc = new(
            OpticalDiscType.Cd,
            "DARK_SIDE_OF_THE_MOON",
            [],
            tracks,
            TimeSpan.FromMinutes(43)
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
            0,
            "Trailer",
            TimeSpan.FromMinutes(2),
            [],
            [],
            [],
            [],
            200_000_000L,
            false
        );

        DiscTitle mainFeature = new(
            1,
            "Main Feature",
            TimeSpan.FromMinutes(120),
            [],
            [],
            [],
            [],
            30_000_000_000L,
            true
        );

        DiscTitle[] titles = [shortTitle, mainFeature];

        DiscTitle longest = titles.MaxBy(t => t.Duration)!;

        longest.IsMainFeature.Should().BeTrue();
        longest.Duration.Should().Be(TimeSpan.FromMinutes(120));
    }

    // ---------------------------------------------------------------------------
    // DiscCandidate
    // ---------------------------------------------------------------------------

    [Fact]
    public void DiscCandidate_ConfidenceRange_IsValid()
    {
        DiscCandidate candidate = new(
            "tmdb",
            "603",
            "The Matrix",
            1999,
            "https://image.tmdb.org/t/p/w500/abc.jpg",
            null,
            0.95,
            MediaType.Movie
        );

        candidate.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
        candidate.Confidence.Should().BeLessThanOrEqualTo(1.0);
        candidate.Source.Should().Be("tmdb");
        candidate.Title.Should().Be("The Matrix");
        candidate.Year.Should().Be(1999);
        candidate.StableId.Should().Be("603");
        candidate.Type.Should().Be(MediaType.Movie);
    }

    [Fact]
    public void DiscCandidate_TvShow_TypeCorrect()
    {
        DiscCandidate candidate = new(
            "tvdb",
            "81189",
            "Breaking Bad",
            2008,
            null,
            null,
            0.88,
            MediaType.TvShow
        );

        candidate.Type.Should().Be(MediaType.TvShow);
        candidate.PosterUrl.Should().BeNull();
    }

    [Fact]
    public void DiscCandidate_Music_TypeCorrect()
    {
        DiscCandidate candidate = new(
            "musicbrainz",
            "a14a5a9f-3c8b-4f5d-bc3e-bb9f4e1e2a8c",
            "The Dark Side of the Moon",
            1973,
            null,
            null,
            0.72,
            MediaType.Music
        );

        candidate.Type.Should().Be(MediaType.Music);
        candidate.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    // ---------------------------------------------------------------------------
    // RipRequest
    // ---------------------------------------------------------------------------

    [Fact]
    public void RipRequest_WithSelectedTitlesAndAudioTracks_ConstructsCorrectly()
    {
        AudioTrackSelection[] audioTracks =
        [
            new(0, true),
            new(1, false),
        ];

        SubtitleSelection[] subtitles =
        [
            new(2, true, SubtitlePolicy.Extract),
        ];

        Ulid libraryId = Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWY");
        Ulid folderId = Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWZ");

        RipRequest request = new(
            "/dev/sr0",
            [0, 1],
            "tmdb:603",
            null,
            libraryId,
            folderId,
            "hd-streaming",
            audioTracks,
            subtitles
        );

        request.DrivePath.Should().Be("/dev/sr0");
        request.SelectedTitleIndices.Should().Equal([0, 1]);
        request.MetadataId.Should().Be("tmdb:603");
        request.Custom.Should().BeNull();
        request.LibraryId.Should().Be(libraryId);
        request.FolderId.Should().Be(folderId);
        request.EncodingProfileId.Should().Be("hd-streaming");
        request.AudioTracks.Should().HaveCount(2);
        request.Subtitles.Should().HaveCount(1);
        request.AudioTracks[0].Include.Should().BeTrue();
        request.AudioTracks[1].Include.Should().BeFalse();
        request.Subtitles[0].Policy.Should().Be(SubtitlePolicy.Extract);
    }

    [Fact]
    public void RipRequest_WithCustomMetadataFallback_ConstructsCorrectly()
    {
        CustomMetadata custom = new(
            "My Home Movie",
            2024,
            MediaType.Movie,
            null
        );

        RipRequest request = new(
            "E:\\",
            [0],
            null,
            custom,
            Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWY"),
            Ulid.Parse("01HMZXX9P3VK7BKFMTQ2AHKGWZ"),
            null,
            [],
            []
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
        AudioTrackSelection included = new(0, true);
        AudioTrackSelection excluded = new(1, false);

        included.StreamIndex.Should().Be(0);
        included.Include.Should().BeTrue();
        excluded.StreamIndex.Should().Be(1);
        excluded.Include.Should().BeFalse();
    }

    [Fact]
    public void SubtitleSelection_ConstructsCorrectly_AllModes()
    {
        SubtitleSelection extract = new(
            0,
            true,
            SubtitlePolicy.Extract
        );
        SubtitleSelection burnIn = new(
            1,
            true,
            SubtitlePolicy.BurnIn
        );
        SubtitleSelection passThrough = new(
            2,
            false,
            SubtitlePolicy.Copy
        );

        extract.Policy.Should().Be(SubtitlePolicy.Extract);
        burnIn.Policy.Should().Be(SubtitlePolicy.BurnIn);
        passThrough.Policy.Should().Be(SubtitlePolicy.Copy);
        passThrough.Include.Should().BeFalse();
    }
}
