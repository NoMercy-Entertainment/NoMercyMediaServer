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

using NoMercy.NmSystem.NewtonSoftConverters;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.DTOs;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public class MusicStartPageData
{
    public TopMusicItemDto? TopArtist { get; set; }
    public TopMusicItemDto? TopAlbum { get; set; }
    public TopMusicItemDto? TopPlaylist { get; set; }
    public List<ArtistCardDto> FavoriteArtists { get; set; } = [];
    public List<AlbumCardDto> FavoriteAlbums { get; set; } = [];
    public List<PlaylistCardDto> Playlists { get; set; } = [];
    public List<ArtistCardDto> LatestArtists { get; set; } = [];
    public List<MusicGenreCardDto> LatestGenres { get; set; } = [];
    public List<AlbumCardDto> LatestAlbums { get; set; } = [];
}

public class ArtistCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Disambiguation { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public Ulid? LibraryId { get; set; }
    public string? Folder { get; set; }
    public int TrackCount { get; set; }
    public string? ThumbImagePath { get; set; }
}

public class AlbumCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Disambiguation { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public Ulid LibraryId { get; set; }
    public string? Folder { get; set; }
    public int Year { get; set; }
    public int TrackCount { get; set; }
    public string? TranslatedDescription { get; set; }
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundImageColorPalette { get; set; }
}

public class PlaylistCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}

public class MusicGenreCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int TrackCount { get; set; }
}

public class TopMusicItemDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string Type { get; set; } = null!;
}

public class SearchTrackCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Ulid? FolderId { get; set; }
    public string? Folder { get; set; }
    public string? Filename { get; set; }
    public string? Cover { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string? Duration { get; set; }
    public int DiscNumber { get; set; }
    public int TrackNumber { get; set; }
    public int? Quality { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Favorite { get; set; }
    public string? AlbumId { get; set; }
    public string? AlbumName { get; set; }
    public string? AlbumCover { get; set; }
    public string? AlbumColorPalette { get; set; }
    public string? ArtistCover { get; set; }
    public string? ArtistColorPalette { get; set; }
    public List<SearchTrackArtistDto> Artists { get; set; } = [];
    public List<SearchTrackAlbumDto> Albums { get; set; } = [];
}

public class SearchTrackArtistDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public class SearchTrackAlbumDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
