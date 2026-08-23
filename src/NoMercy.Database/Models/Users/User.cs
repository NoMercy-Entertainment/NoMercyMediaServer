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

[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Name))]
[Index(nameof(Allowed))]
[Index(nameof(Owner))]
[Index(nameof(Manage))]
[Index(nameof(OpticalAccess))]
[PrimaryKey(nameof(Id))]
public class User : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("manage")]
    public bool Manage { get; set; }

    [JsonProperty("owner")]
    public bool Owner { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("allowed")]
    public bool Allowed { get; set; }

    /// <summary>
    /// Read-only physical optical drive access: probing discs, browsing
    /// titles, and watching live — never ripping, ejecting, or changing
    /// server configuration. Distinct from <see cref="Allowed"/> (full
    /// library access) since a household member might get one without
    /// the other.
    /// </summary>
    [JsonProperty("optical_access")]
    public bool OpticalAccess { get; set; }

    [JsonProperty("audio_transcoding")]
    public bool AudioTranscoding { get; set; }

    [JsonProperty("video_transcoding")]
    public bool VideoTranscoding { get; set; }

    [JsonProperty("no_transcoding")]
    public bool NoTranscoding { get; set; }

    [JsonProperty("library_user")]
    public virtual ICollection<LibraryUser> LibraryUser { get; set; } = [];

    [JsonProperty("movie_user")]
    public virtual ICollection<MovieUser> MovieUser { get; set; } = [];

    [JsonProperty("tv_user")]
    public virtual ICollection<TvUser> TvUser { get; set; } = [];

    [JsonProperty("collection_user")]
    public virtual ICollection<CollectionUser> CollectionUser { get; set; } = [];

    [JsonProperty("special_user")]
    public virtual ICollection<SpecialUser> SpecialUser { get; set; } = [];

    [JsonProperty("notification_user")]
    public virtual ICollection<NotificationUser> NotificationUser { get; set; } = [];

    [JsonProperty("album_user")]
    public virtual ICollection<AlbumUser> AlbumUser { get; set; } = [];

    [JsonProperty("artist_user")]
    public virtual ICollection<ArtistUser> ArtistUser { get; set; } = [];

    [JsonProperty("track_user")]
    public virtual ICollection<TrackUser> TrackUser { get; set; } = [];

    [JsonProperty("playback_preferences")]
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
