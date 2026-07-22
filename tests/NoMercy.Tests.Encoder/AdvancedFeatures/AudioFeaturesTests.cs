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

using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

public class AudioFeaturesTests
{
    [Fact]
    public void LoudnessMode_HasExpectedValues()
    {
        LoudnessMode[] values = Enum.GetValues<LoudnessMode>();

        values.Should().Contain(expected: LoudnessMode.None);
        values.Should().Contain(expected: LoudnessMode.EbuR128);
        values.Should().Contain(expected: LoudnessMode.ReplayGain);
        values.Should().Contain(expected: LoudnessMode.Custom);
        values.Should().HaveCount(expected: 4);
    }

    [Fact]
    public void LoudnessMode_DefaultIsNone()
    {
        LoudnessMode mode = default;
        mode.Should().Be(expected: LoudnessMode.None);
    }

    [Fact]
    public void AudioMetadata_ConstructsWithRequiredFields()
    {
        AudioMetadata metadata = new(
            Title: "Track One",
            Artist: "Some Artist",
            AlbumArtist: "Various Artists",
            Album: "The Album",
            TrackNumber: 1,
            DiscNumber: 1,
            Year: 2024,
            Genre: "Rock",
            MusicBrainzTrackId: "mbid-track-001",
            MusicBrainzReleaseId: "mbid-release-001",
            AcoustIdFingerprint: "aqstid-fp-001",
            CoverArt: null
        );

        metadata.Title.Should().Be(expected: "Track One");
        metadata.Artist.Should().Be(expected: "Some Artist");
        metadata.AlbumArtist.Should().Be(expected: "Various Artists");
        metadata.Album.Should().Be(expected: "The Album");
        metadata.TrackNumber.Should().Be(expected: 1);
        metadata.DiscNumber.Should().Be(expected: 1);
        metadata.Year.Should().Be(expected: 2024);
        metadata.Genre.Should().Be(expected: "Rock");
        metadata.MusicBrainzTrackId.Should().Be(expected: "mbid-track-001");
        metadata.MusicBrainzReleaseId.Should().Be(expected: "mbid-release-001");
        metadata.AcoustIdFingerprint.Should().Be(expected: "aqstid-fp-001");
        metadata.CoverArt.Should().BeNull();
    }

    [Fact]
    public void AudioMetadata_SupportsNullableOptionalFields()
    {
        AudioMetadata metadata = new(
            Title: "Minimal Track",
            Artist: "Artist",
            AlbumArtist: "Artist",
            Album: "Album",
            TrackNumber: 1,
            DiscNumber: 1,
            Year: null,
            Genre: null,
            MusicBrainzTrackId: null,
            MusicBrainzReleaseId: null,
            AcoustIdFingerprint: null,
            CoverArt: null
        );

        metadata.Year.Should().BeNull();
        metadata.Genre.Should().BeNull();
        metadata.MusicBrainzTrackId.Should().BeNull();
        metadata.AcoustIdFingerprint.Should().BeNull();
        metadata.CoverArt.Should().BeNull();
    }

    [Fact]
    public void AlbumArtSource_ConstructsWithFilePath()
    {
        AlbumArtSource source = new(
            FilePath: "/music/covers/album.jpg",
            Url: null,
            Type: AlbumArtType.Front
        );

        source.FilePath.Should().Be(expected: "/music/covers/album.jpg");
        source.Url.Should().BeNull();
        source.Type.Should().Be(expected: AlbumArtType.Front);
    }

    [Fact]
    public void AlbumArtSource_ConstructsWithUrl()
    {
        AlbumArtSource source = new(
            FilePath: null,
            Url: "https://example.com/cover.jpg",
            Type: AlbumArtType.Artist
        );

        source.FilePath.Should().BeNull();
        source.Url.Should().Be(expected: "https://example.com/cover.jpg");
        source.Type.Should().Be(expected: AlbumArtType.Artist);
    }

    [Fact]
    public void AlbumArtType_HasExpectedValues()
    {
        AlbumArtType[] values = Enum.GetValues<AlbumArtType>();

        values.Should().Contain(expected: AlbumArtType.Front);
        values.Should().Contain(expected: AlbumArtType.Back);
        values.Should().Contain(expected: AlbumArtType.Disc);
        values.Should().Contain(expected: AlbumArtType.Artist);
        values.Should().Contain(expected: AlbumArtType.Other);
        values.Should().HaveCount(expected: 5);
    }

    [Fact]
    public void AudioMetadata_WithCoverArt_CarriesCoverArt()
    {
        AlbumArtSource coverArt = new(
            FilePath: "/tmp/cover.jpg",
            Url: null,
            Type: AlbumArtType.Front
        );

        AudioMetadata metadata = new(
            Title: "Track",
            Artist: "Artist",
            AlbumArtist: "Artist",
            Album: "Album",
            TrackNumber: 1,
            DiscNumber: 1,
            Year: 2024,
            Genre: "Jazz",
            MusicBrainzTrackId: null,
            MusicBrainzReleaseId: null,
            AcoustIdFingerprint: null,
            CoverArt: coverArt
        );

        metadata.CoverArt.Should().NotBeNull();
        metadata.CoverArt!.Type.Should().Be(expected: AlbumArtType.Front);
        metadata.CoverArt.FilePath.Should().Be(expected: "/tmp/cover.jpg");
    }
}
