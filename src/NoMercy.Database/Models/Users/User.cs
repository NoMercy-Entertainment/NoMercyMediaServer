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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[Index(propertyName: nameof(Email), IsUnique = true)]
[Index(propertyName: nameof(Name))]
[Index(propertyName: nameof(Allowed))]
[Index(propertyName: nameof(Owner))]
[Index(propertyName: nameof(Manage))]
[PrimaryKey(propertyName: nameof(Id))]
public class User : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty(propertyName: "manage")]
    public bool Manage { get; set; }

    [JsonProperty(propertyName: "owner")]
    public bool Owner { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "allowed")]
    public bool Allowed { get; set; }

    [JsonProperty(propertyName: "audio_transcoding")]
    public bool AudioTranscoding { get; set; }

    [JsonProperty(propertyName: "video_transcoding")]
    public bool VideoTranscoding { get; set; }

    [JsonProperty(propertyName: "no_transcoding")]
    public bool NoTranscoding { get; set; }

    [JsonProperty(propertyName: "library_user")]
    public virtual ICollection<LibraryUser> LibraryUser { get; set; } = [];

    [JsonProperty(propertyName: "movie_user")]
    public virtual ICollection<MovieUser> MovieUser { get; set; } = [];

    [JsonProperty(propertyName: "tv_user")]
    public virtual ICollection<TvUser> TvUser { get; set; } = [];

    [JsonProperty(propertyName: "collection_user")]
    public virtual ICollection<CollectionUser> CollectionUser { get; set; } = [];

    [JsonProperty(propertyName: "special_user")]
    public virtual ICollection<SpecialUser> SpecialUser { get; set; } = [];

    [JsonProperty(propertyName: "notification_user")]
    public virtual ICollection<NotificationUser> NotificationUser { get; set; } = [];

    [JsonProperty(propertyName: "album_user")]
    public virtual ICollection<AlbumUser> AlbumUser { get; set; } = [];

    [JsonProperty(propertyName: "artist_user")]
    public virtual ICollection<ArtistUser> ArtistUser { get; set; } = [];

    [JsonProperty(propertyName: "track_user")]
    public virtual ICollection<TrackUser> TrackUser { get; set; } = [];

    [JsonProperty(propertyName: "playback_preferences")]
    public virtual ICollection<PlaybackPreference> PlaybackPreferences { get; set; } = [];

    public User()
    {
        //
    }

    public User(
        Guid id,
        string email,
        bool manage,
        bool owner,
        string name,
        bool allowed,
        bool audioTranscoding,
        bool videoTranscoding,
        bool noTranscoding
    )
    {
        Id = id;
        Email = email;
        Manage = manage;
        Owner = owner;
        Name = name;
        Allowed = allowed;
        AudioTranscoding = audioTranscoding;
        VideoTranscoding = videoTranscoding;
        NoTranscoding = noTranscoding;
    }
}
