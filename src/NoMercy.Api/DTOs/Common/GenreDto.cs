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

using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Common;

public record GenreDto
{
    [JsonProperty(propertyName: "id")]
    public dynamic Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    public GenreDto()
    {
        Id = 0;
        Name = string.Empty;
        Link = new(uriString: "/genres/0", uriKind: UriKind.Relative);
    }

    public GenreDto(GenreMovie genreMovie)
    {
        Id = genreMovie.GenreId;
        Name = genreMovie.Genre.Name;
        Link = new(uriString: $"/genres/{Id}", uriKind: UriKind.Relative);
    }

    public GenreDto(GenreTv genreTv)
    {
        Id = genreTv.GenreId;
        Name = genreTv.Genre.Name;
        Link = new(uriString: $"/genres/{Id}", uriKind: UriKind.Relative);
    }

    public GenreDto(Genre genreMovie)
    {
        Id = genreMovie.Id;
        Name = genreMovie.Name;
        Link = new(uriString: $"/genres/{Id}", uriKind: UriKind.Relative);
    }

    public GenreDto(TmdbGenre tmdbGenreMovie)
    {
        Id = tmdbGenreMovie.Id;
        Name = tmdbGenreMovie.Name.OrEmpty();
        Link = new(uriString: $"/genres/{Id}", uriKind: UriKind.Relative);
    }

    public GenreDto(ArtistMusicGenre artistMusicGenre)
    {
        Id = artistMusicGenre.MusicGenreId;
        Name = artistMusicGenre.MusicGenre.Name;
        Link = new(uriString: $"/music/genres/{Id}", uriKind: UriKind.Relative);
    }

    public GenreDto(AlbumMusicGenre artistMusicGenre)
    {
        Id = artistMusicGenre.MusicGenreId;
        Name = artistMusicGenre.MusicGenre.Name;
        Link = new(uriString: $"/music/genres/{Id}", uriKind: UriKind.Relative);
    }
}
