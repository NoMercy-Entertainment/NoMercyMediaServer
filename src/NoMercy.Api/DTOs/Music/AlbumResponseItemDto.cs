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
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record AlbumResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "artists")]
    public IEnumerable<ArtistDto> Artists { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public IEnumerable<AlbumTrackDto> Tracks { get; set; }

    [JsonProperty(propertyName: "images")]
    public IEnumerable<ImageDto> Images { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    public AlbumResponseItemDto(Album album, string? country = "US")
    {
        ColorPalette = album.ColorPalette;
        Cover = !string.IsNullOrEmpty(value: album.Cover)
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Favorite = album.AlbumUser.Count != 0;
        Id = album.Id;
        LibraryId = album.LibraryId;
        Name = album.Name;
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Type = "album";

        Artists = album
            .AlbumArtist.DistinctBy(keySelector: trackArtist => trackArtist.ArtistId)
            .Select(selector: albumArtist => new ArtistDto(albumArtist: albumArtist, country: country!));

        Genres = album.AlbumMusicGenre.Select(selector: musicGenre => new GenreDto(artistMusicGenre: musicGenre));

        Images = album.Images.Select(selector: image => new ImageDto(media: image));

        Tracks = album.AlbumTrack.Select(selector: albumTrack => new AlbumTrackDto(albumTrack: albumTrack, country: country!));
    }
}
