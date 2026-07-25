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

        values.Should().Contain(LoudnessMode.None);
        values.Should().Contain(LoudnessMode.EbuR128);
        values.Should().Contain(LoudnessMode.ReplayGain);
        values.Should().Contain(LoudnessMode.Custom);
        values.Should().HaveCount(4);
    }

    [Fact]
    public void LoudnessMode_DefaultIsNone()
    {
        LoudnessMode mode = default;
        mode.Should().Be(LoudnessMode.None);
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

        metadata.Title.Should().Be("Track One");
        metadata.Artist.Should().Be("Some Artist");
        metadata.AlbumArtist.Should().Be("Various Artists");
        metadata.Album.Should().Be("The Album");
        metadata.TrackNumber.Should().Be(1);
        metadata.DiscNumber.Should().Be(1);
        metadata.Year.Should().Be(2024);
        metadata.Genre.Should().Be("Rock");
        metadata.MusicBrainzTrackId.Should().Be("mbid-track-001");
        metadata.MusicBrainzReleaseId.Should().Be("mbid-release-001");
        metadata.AcoustIdFingerprint.Should().Be("aqstid-fp-001");
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

        source.FilePath.Should().Be("/music/covers/album.jpg");
        source.Url.Should().BeNull();
        source.Type.Should().Be(AlbumArtType.Front);
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
        source.Url.Should().Be("https://example.com/cover.jpg");
        source.Type.Should().Be(AlbumArtType.Artist);
    }

    [Fact]
    public void AlbumArtType_HasExpectedValues()
    {
        AlbumArtType[] values = Enum.GetValues<AlbumArtType>();

        values.Should().Contain(AlbumArtType.Front);
        values.Should().Contain(AlbumArtType.Back);
        values.Should().Contain(AlbumArtType.Disc);
        values.Should().Contain(AlbumArtType.Artist);
        values.Should().Contain(AlbumArtType.Other);
        values.Should().HaveCount(5);
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
        metadata.CoverArt!.Type.Should().Be(AlbumArtType.Front);
        metadata.CoverArt.FilePath.Should().Be("/tmp/cover.jpg");
    }
}
