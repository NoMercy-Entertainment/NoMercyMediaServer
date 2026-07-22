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
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Common;

public class PlaylistDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }

    [JsonProperty(propertyName: "user")]
    public User User { get; set; }

    [JsonProperty(propertyName: "playlist_track")]
    public ICollection<PlaylistTrack> Tracks { get; set; }

    public PlaylistDto(Playlist playlist)
    {
        Id = playlist.Id;
        Name = playlist.Name;
        Description = playlist.Description;
        Cover = playlist.Cover;
        Cover = Cover is not null
            ? new Uri(uriString: $"/images/music{Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Filename = playlist.Filename;
        Duration = playlist.Duration;
        UserId = playlist.UserId;
        User = playlist.User;
        Tracks = playlist.Tracks;
    }
}
