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
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record TracksResponseItemDto
{
    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "cover")]
    public Uri? Cover { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "artists")]
    public List<ArtistDto> Artists { get; set; } = [];

    [JsonProperty(propertyName: "albums")]
    public List<AlbumDto> Albums { get; set; } = [];

    [JsonProperty(propertyName: "tracks")]
    public List<ArtistTrackDto> Tracks { get; set; } = [];

    public TracksResponseItemDto()
    {
        //
    }

    public TracksResponseItemDto(Track track, string country)
    {
        Id = track.Id;
        Name = track.Name;
        Cover = track.Cover is not null
            ? new Uri(uriString: $"/images/music{track.Cover}", uriKind: UriKind.Relative)
            : null;
        Link = new(uriString: $"/music/tracks/{track.Id}", uriKind: UriKind.Relative);

        ColorPalette = track.ColorPalette;
        Favorite = track.TrackUser.Count != 0;
        Type = "favorites";

        Artists = track
            .ArtistTrack.Select(selector: trackArtist => new ArtistDto(artistTrack: trackArtist, country: country))
            .ToList();

        Albums = track.AlbumTrack.Select(selector: albumTrack => new AlbumDto(albumTrack: albumTrack, country: country)).ToList();

        Tracks = track
            .ArtistTrack.Select(selector: albumTrack => new ArtistTrackDto(artistTrack: albumTrack, country: country))
            .ToList();
    }
}
