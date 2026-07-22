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
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public class FavoriteTrackDto
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
    public JToken? ColorPalette { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "album_artist")]
    public Guid? AlbumArtist { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "album_track")]
    public IEnumerable<AlbumDto> Albums { get; set; }

    [JsonProperty(propertyName: "artist_track")]
    public IEnumerable<ArtistDto> Artists { get; set; }

    public FavoriteTrackDto(ArtistTrack artistTrack, string country)
    {
        Id = artistTrack.Track.Id;
        Name = artistTrack.Track.Name;
        Cover = artistTrack.Track.Cover is not null
            ? new Uri(uriString: $"/images/music{artistTrack.Track.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Link = new(uriString: $"/music/tracks/{Id}", uriKind: UriKind.Relative);
        Type = "track";
        ColorPalette = artistTrack.Track._colorPalette.ToRaw();
        Year = artistTrack.Track.Date.ParseYear();

        Albums = artistTrack.Track.AlbumTrack.Select(selector: albumTrack => new AlbumDto(
            albumTrack: albumTrack,
            country: country
        ));
        Artists = artistTrack.Track.ArtistTrack.Select(selector: albumTrack => new ArtistDto(
            artistTrack: albumTrack,
            country: country
        ));
    }
}
