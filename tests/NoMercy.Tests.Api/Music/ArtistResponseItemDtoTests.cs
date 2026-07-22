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

using NoMercy.Api.DTOs.Music;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api.Music;

[Trait(name: "Category", value: "Unit")]
public class ArtistResponseItemDtoTests
{
    private static Artist BuildMinimalArtist(
        Guid? id = null,
        Translation[]? translations = null,
        Image[]? images = null,
        ArtistUser[]? artistUsers = null,
        ArtistMusicGenre[]? genres = null,
        AlbumArtist[]? albumArtists = null,
        ArtistTrack[]? artistTracks = null,
        string? cover = null,
        ColorPalette? colorPalette = null,
        string? folder = "/music/artist-folder",
        Ulid? libraryId = null,
        string description = "Fallback bio"
    )
    {
        Artist artist = new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test Artist",
            Description = description,
            Disambiguation = "The Band",
            Cover = cover,
            Folder = folder,
            LibraryId = libraryId ?? Ulid.NewUlid(),
        };
        foreach (Translation translation in translations ?? [])
            artist.Translations.Add(item: translation);
        foreach (Image image in images ?? [])
            artist.Images.Add(item: image);
        foreach (ArtistUser artistUser in artistUsers ?? [])
            artist.ArtistUser.Add(item: artistUser);
        foreach (ArtistMusicGenre genre in genres ?? [])
            artist.ArtistMusicGenre.Add(item: genre);
        foreach (AlbumArtist albumArtist in albumArtists ?? [])
            artist.AlbumArtist.Add(item: albumArtist);
        foreach (ArtistTrack artistTrack in artistTracks ?? [])
            artist.ArtistTrack.Add(item: artistTrack);
        if (colorPalette is not null)
            artist.ColorPalette = colorPalette;

        return artist;
    }

    private static AlbumArtist BuildAlbumArtist(
        int year,
        string albumName = "Album",
        AlbumUser[]? albumUsers = null
    )
    {
        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = albumName,
            Year = year,
        };
        foreach (AlbumUser albumUser in albumUsers ?? [])
            album.AlbumUser.Add(item: albumUser);
        Artist nestedArtist = new() { Id = Guid.NewGuid(), Name = "Nested Artist" };

        return new()
        {
            AlbumId = album.Id,
            Album = album,
            ArtistId = nestedArtist.Id,
            Artist = nestedArtist,
        };
    }

    private static ArtistTrack BuildArtistTrack(
        Guid artistId,
        Album? linkedAlbum = null,
        TrackUser[]? trackUsers = null,
        string trackName = "Song",
        int disc = 1,
        int trackNumber = 1
    )
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = trackName,
            Folder = "/folder",
            Filename = "song.flac",
            FolderId = Ulid.NewUlid(),
            DiscNumber = disc,
            TrackNumber = trackNumber,
            Duration = "200",
        };
        foreach (TrackUser trackUser in trackUsers ?? [])
            track.TrackUser.Add(item: trackUser);
        if (linkedAlbum is not null)
            track.AlbumTrack.Add(
                item: new()
                {
                    AlbumId = linkedAlbum.Id,
                    Album = linkedAlbum,
                    Track = track,
                }
            );

        return new() { ArtistId = artistId, Track = track };
    }

    private static ColorPalette? ParsePalette(Newtonsoft.Json.Linq.JToken? token) =>
        token is null ? null : ColorPalette.FromJsonOrNull(json: token.ToString());

    // =========================================================================
    // Description: translation-match branch vs fallback (line ~92)
    // =========================================================================

    [Fact]
    public void Ctor_DescriptionFromMatchingTranslation()
    {
        Artist artist = BuildMinimalArtist(
            translations: [new() { Iso31661 = "NL", Description = "Vertaalde bio" }],
            description: "English bio"
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid(), country: "NL");

        dto.Description.Should().Be(expected: "Vertaalde bio");
    }

    [Fact]
    public void Ctor_DescriptionFallsBackWhenNoTranslationMatch()
    {
        Artist artist = BuildMinimalArtist(
            translations: [new() { Iso31661 = "DE", Description = "Deutsche Bio" }],
            description: "English bio"
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid(), country: "NL");

        dto.Description.Should().Be(expected: "English bio");
    }

    // =========================================================================
    // Backdrop / background image
    // =========================================================================

    [Fact]
    public void Ctor_BackdropFromBackgroundImage()
    {
        Artist artist = BuildMinimalArtist(
            images: [new() { Type = "background", FilePath = "/bg.jpg" }]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Backdrop.Should().Be(expected: "/images/music/bg.jpg");
    }

    [Fact]
    public void Ctor_BackdropNull_WhenNoBackgroundImage()
    {
        Artist artist = BuildMinimalArtist(
            images: [new() { Type = "thumb", FilePath = "/thumb.jpg" }]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Backdrop.Should().BeNull();
    }

    // =========================================================================
    // Cover: artist.Cover vs highest-voted "thumb" image (line ~96-97)
    // =========================================================================

    [Fact]
    public void Ctor_CoverFromArtistCover_WhenPresent()
    {
        Artist artist = BuildMinimalArtist(
            cover: "/artist-cover.jpg",
            images:
            [
                new()
                {
                    Type = "thumb",
                    FilePath = "/thumb.jpg",
                    VoteAverage = 9,
                },
            ]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Cover.Should().Be(expected: "/images/music/artist-cover.jpg");
    }

    [Fact]
    public void Ctor_CoverFallsBackToHighestVotedThumbImage()
    {
        Image lowVoteThumb = new()
        {
            Type = "thumb",
            FilePath = "/low-thumb.jpg",
            VoteAverage = 2,
        };
        Image highVoteThumb = new()
        {
            Type = "thumb",
            FilePath = "/high-thumb.jpg",
            VoteAverage = 9,
        };
        Image higherVotedPoster = new()
        {
            Type = "poster",
            FilePath = "/poster.jpg",
            VoteAverage = 20,
        };
        Artist artist = BuildMinimalArtist(
            cover: null,
            images: [lowVoteThumb, highVoteThumb, higherVotedPoster]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Cover.Should().Be(expected: "/images/music/high-thumb.jpg");
    }

    [Fact]
    public void Ctor_CoverNull_WhenNoArtistCoverNorThumbImage()
    {
        Artist artist = BuildMinimalArtist(cover: null, images: []);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Cover.Should().BeNull();
    }

    // =========================================================================
    // ColorPalette: artist palette vs thumb-image palette fallback
    // =========================================================================

    [Fact]
    public void Ctor_ColorPaletteFromArtist_WhenPresent()
    {
        ColorPalette palette = new() { Cover = new() { Dominant = "#111111" } };
        Artist artist = BuildMinimalArtist(colorPalette: palette);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        ParsePalette(token: dto.ColorPalette)!.Cover!.Dominant.Should().Be(expected: "#111111");
    }

    [Fact]
    public void Ctor_ColorPaletteFallsBackToThumbPalette_WhenArtistPaletteEmpty()
    {
        Image thumb = new()
        {
            Type = "thumb",
            FilePath = "/thumb.jpg",
            VoteAverage = 5,
        };
        thumb.ColorPalette = new() { Cover = new() { Dominant = "#222222" } };
        Artist artist = BuildMinimalArtist(images: [thumb], colorPalette: null);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        ParsePalette(token: dto.ColorPalette)!.Cover!.Dominant.Should().Be(expected: "#222222");
    }

    // =========================================================================
    // Favorite
    // =========================================================================

    [Fact]
    public void Ctor_FavoriteTrue_WhenArtistUserHasEntries()
    {
        ArtistUser artistUser = new(artistId: Guid.NewGuid(), userId: Guid.NewGuid());
        Artist artist = BuildMinimalArtist(artistUsers: [artistUser]);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Favorite.Should().BeTrue();
    }

    [Fact]
    public void Ctor_FavoriteFalse_WhenNoArtistUserEntries()
    {
        Artist artist = BuildMinimalArtist(artistUsers: []);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Favorite.Should().BeFalse();
    }

    // =========================================================================
    // Identity fields
    // =========================================================================

    [Fact]
    public void Ctor_SetsIdFolderLibraryIdNameTypeLinkDisambiguation()
    {
        Ulid libraryId = Ulid.NewUlid();
        Artist artist = BuildMinimalArtist(folder: "/music/my-artist", libraryId: libraryId);
        Guid artistId = artist.Id;

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Id.Should().Be(expected: artistId);
        dto.Folder.Should().Be(expected: "/music/my-artist");
        dto.LibraryId.Should().Be(expected: libraryId);
        dto.Name.Should().Be(expected: "Test Artist");
        dto.Type.Should().Be(expected: "artist");
        dto.Link.ToString().Should().Be(expected: $"/music/artists/{artistId}");
        dto.Disambiguation.Should().Be(expected: "The Band");
    }

    // =========================================================================
    // Genres / Images passthrough collections
    // =========================================================================

    [Fact]
    public void Ctor_GenresMappedFromArtistMusicGenre()
    {
        MusicGenre genre = new() { Id = Guid.NewGuid(), Name = "Rock" };
        ArtistMusicGenre artistGenre = new() { MusicGenreId = genre.Id, MusicGenre = genre };
        Artist artist = BuildMinimalArtist(genres: [artistGenre]);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Genres.Should().ContainSingle(predicate: g => g.Name == "Rock");
    }

    [Fact]
    public void Ctor_ImagesMappedFromArtistImages()
    {
        Image image = new()
        {
            Type = "poster",
            FilePath = "/poster.jpg",
            Width = 300,
            Height = 450,
        };
        Artist artist = BuildMinimalArtist(images: [image]);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Images.Should().ContainSingle();
        dto.Images.First().Type.Should().Be(expected: "poster");
        dto.Images.First().Width.Should().Be(expected: 300);
    }

    // =========================================================================
    // Albums: GroupBy(Id).Select(First) dedup + final OrderBy(Year) (line ~132-137)
    // =========================================================================

    [Fact]
    public void Ctor_AlbumsDedupedByIdAndOrderedByYear()
    {
        AlbumArtist newerAlbum = BuildAlbumArtist(year: 2015, albumName: "Newer Album");
        AlbumArtist olderAlbum = BuildAlbumArtist(year: 2005, albumName: "Older Album");
        AlbumArtist duplicateOfNewer = new()
        {
            AlbumId = newerAlbum.AlbumId,
            Album = newerAlbum.Album,
            ArtistId = Guid.NewGuid(),
            Artist = new() { Id = Guid.NewGuid(), Name = "Other Nested" },
        };
        Artist artist = BuildMinimalArtist(
            albumArtists: [newerAlbum, olderAlbum, duplicateOfNewer]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Albums.Should().HaveCount(expected: 2);
        dto.Albums.Select(selector: album => album.Year).Should().Equal(elements: [2005, 2015]);
    }

    // =========================================================================
    // Featured: dedup-against-Albums predicate (line ~139-148)
    // =========================================================================

    [Fact]
    public void Ctor_FeaturedExcludesAlbumsAlreadyInAlbums_ButIncludesOthers()
    {
        Guid artistId = Guid.NewGuid();

        Album albumInLibrary = new()
        {
            Id = Guid.NewGuid(),
            Name = "Album X",
            Year = 2010,
        };
        AlbumArtist albumArtistX = new()
        {
            AlbumId = albumInLibrary.Id,
            Album = albumInLibrary,
            ArtistId = artistId,
            Artist = new() { Id = artistId, Name = "Test Artist" },
        };

        Album trackOnlyAlbum = new()
        {
            Id = Guid.NewGuid(),
            Name = "Album Y",
            Year = 2012,
        };

        ArtistTrack trackOnAlbumX = BuildArtistTrack(
            artistId: artistId,
            linkedAlbum: albumInLibrary,
            trackName: "Track On X"
        );
        ArtistTrack trackOnAlbumY = BuildArtistTrack(
            artistId: artistId,
            linkedAlbum: trackOnlyAlbum,
            trackName: "Track On Y"
        );

        Artist artist = BuildMinimalArtist(
            id: artistId,
            albumArtists: [albumArtistX],
            artistTracks: [trackOnAlbumX, trackOnAlbumY]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Albums.Select(selector: album => album.Name).Should().Contain(expected: "Album X");
        dto.Featured.Select(selector: album => album.Name).Should().Contain(expected: "Album Y");
        dto.Featured.Select(selector: album => album.Name).Should().NotContain(unexpected: "Album X");
    }

    // =========================================================================
    // Playlists: AlbumUser row matching the passed-in userId (line ~150-155)
    // =========================================================================

    [Fact]
    public void Ctor_PlaylistsOnlyIncludesAlbumsLikedByGivenUser()
    {
        Guid likedUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        AlbumArtist likedAlbumArtist = BuildAlbumArtist(
            year: 2018,
            albumName: "Liked Album",
            albumUsers: [new(albumId: Guid.NewGuid(), userId: likedUserId)]
        );
        AlbumArtist notLikedAlbumArtist = BuildAlbumArtist(
            year: 2019,
            albumName: "Not Liked Album",
            albumUsers: [new(albumId: Guid.NewGuid(), userId: otherUserId)]
        );

        Artist artist = BuildMinimalArtist(albumArtists: [likedAlbumArtist, notLikedAlbumArtist]);

        ArtistResponseItemDto dto = new(artist: artist, userId: likedUserId);

        dto.Playlists.Should().ContainSingle(predicate: album => album.Name == "Liked Album");
    }

    [Fact]
    public void Ctor_PlaylistsDistinctByAlbumId()
    {
        Guid userId = Guid.NewGuid();
        AlbumArtist sharedAlbumRowOne = BuildAlbumArtist(
            year: 2018,
            albumName: "Liked Album",
            albumUsers: [new(albumId: Guid.NewGuid(), userId: userId)]
        );
        AlbumArtist sharedAlbumRowTwo = new()
        {
            AlbumId = sharedAlbumRowOne.AlbumId,
            Album = sharedAlbumRowOne.Album,
            ArtistId = Guid.NewGuid(),
            Artist = new() { Id = Guid.NewGuid(), Name = "Other Nested" },
        };

        Artist artist = BuildMinimalArtist(albumArtists: [sharedAlbumRowOne, sharedAlbumRowTwo]);

        ArtistResponseItemDto dto = new(artist: artist, userId: userId);

        dto.Playlists.Should().ContainSingle();
    }

    // =========================================================================
    // Tracks: DistinctBy(Id) + OrderBy(AlbumName).ThenBy(Disc).ThenBy(Track)
    // =========================================================================

    [Fact]
    public void Ctor_TracksDedupedByIdAndOrderedByAlbumNameThenDiscThenTrack()
    {
        Guid artistId = Guid.NewGuid();
        Album albumZeta = new() { Id = Guid.NewGuid(), Name = "Zeta Album" };
        Album albumAlpha = new() { Id = Guid.NewGuid(), Name = "Alpha Album" };

        ArtistTrack trackZeta = BuildArtistTrack(
            artistId: artistId,
            linkedAlbum: albumZeta,
            trackName: "Song Z",
            disc: 1,
            trackNumber: 5
        );
        ArtistTrack trackAlphaDiscTwo = BuildArtistTrack(
            artistId: artistId,
            linkedAlbum: albumAlpha,
            trackName: "Song A2",
            disc: 2,
            trackNumber: 1
        );
        ArtistTrack trackAlphaDiscOne = BuildArtistTrack(
            artistId: artistId,
            linkedAlbum: albumAlpha,
            trackName: "Song A1",
            disc: 1,
            trackNumber: 9
        );
        // Duplicate join row pointing at the same underlying Track, to exercise
        // the DistinctBy(artistTrack => artistTrack.Id) dedup.
        ArtistTrack duplicateOfAlphaDiscOne = new()
        {
            ArtistId = artistId,
            Track = trackAlphaDiscOne.Track,
        };

        Artist artist = BuildMinimalArtist(
            id: artistId,
            artistTracks: [trackZeta, trackAlphaDiscTwo, trackAlphaDiscOne, duplicateOfAlphaDiscOne]
        );

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.Tracks.Should().HaveCount(expected: 3);
        dto.Tracks.Select(selector: track => track.Name).Should().Equal(expected: ["Song A1", "Song A2", "Song Z"]);
    }

    // =========================================================================
    // FavoriteTracks: only tracks with a TrackUser "liked" row
    // =========================================================================

    [Fact]
    public void Ctor_FavoriteTracksOnlyIncludesTracksWithTrackUserEntries()
    {
        Guid artistId = Guid.NewGuid();
        TrackUser trackUser = new(trackId: Guid.NewGuid(), userId: Guid.NewGuid());
        ArtistTrack likedTrack = BuildArtistTrack(
            artistId: artistId,
            trackUsers: [trackUser],
            trackName: "Liked Song"
        );
        ArtistTrack notLikedTrack = BuildArtistTrack(artistId: artistId, trackName: "Not Liked Song");

        Artist artist = BuildMinimalArtist(id: artistId, artistTracks: [likedTrack, notLikedTrack]);

        ArtistResponseItemDto dto = new(artist: artist, userId: Guid.NewGuid());

        dto.FavoriteTracks.Should().ContainSingle(predicate: track => track.Name == "Liked Song");
    }
}
