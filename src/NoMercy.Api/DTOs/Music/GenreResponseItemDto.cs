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
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Music;

public record GenreResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid? LibraryId { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public IEnumerable<GenreTrackDto> Tracks { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    public GenreResponseItemDto(MusicGenre genre, string? country = "US")
    {
        Id = genre.Id;
        Name = genre.Name.ToTitleCase();
        Link = new(uriString: $"/music/genres/{Id}", uriKind: UriKind.Relative);
        Type = "genre";
        Tracks = genre
            .MusicGenreTracks.Select(selector: genreTrack => new GenreTrackDto(genreTrack: genreTrack, country: country!))
            .OrderBy(keySelector: genreTrack => genreTrack.Disc)
            .ThenBy(keySelector: genreTrack => genreTrack.Track);
    }
}
