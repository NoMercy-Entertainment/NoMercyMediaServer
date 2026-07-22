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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchMusixMatchTrack
{
    [JsonProperty(propertyName: "track_id")]
    public long TrackId { get; set; }

    [JsonProperty(propertyName: "track_mbid")]
    public string TrackMbid { get; set; } = string.Empty;

    [JsonProperty(propertyName: "track_isrc")]
    public string TrackIsrc { get; set; } = string.Empty;

    [JsonProperty(propertyName: "commontrack_isrcs")]
    public string[][] CommontrackIsrcs { get; set; } = [];

    [JsonProperty(propertyName: "track_spotify_id")]
    public string TrackSpotifyId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "commontrack_spotify_ids")]
    public string[] CommontrackSpotifyIds { get; set; } = [];

    [JsonProperty(propertyName: "commontrack_itunes_ids")]
    public long[] CommontrackItunesIds { get; set; } = [];

    [JsonProperty(propertyName: "track_soundcloud_id")]
    public long TrackSoundcloudId { get; set; }

    [JsonProperty(propertyName: "track_xboxmusic_id")]
    public string TrackXboxmusicId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "track_name")]
    public string TrackName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "track_name_translation_list")]
    public object[] TrackNameTranslationList { get; set; } = [];

    [JsonProperty(propertyName: "track_rating")]
    public long TrackRating { get; set; }

    [JsonProperty(propertyName: "track_length")]
    public long TrackLength { get; set; }

    [JsonProperty(propertyName: "commontrack_id")]
    public long CommontrackId { get; set; }

    [JsonProperty(propertyName: "instrumental")]
    public long Instrumental { get; set; }

    [JsonProperty(propertyName: "explicit")]
    public long Explicit { get; set; }

    [JsonProperty(propertyName: "has_lyrics")]
    public long HasLyrics { get; set; }

    [JsonProperty(propertyName: "has_lyrics_crowd")]
    public long HasLyricsCrowd { get; set; }

    [JsonProperty(propertyName: "has_subtitles")]
    public long HasSubtitles { get; set; }

    [JsonProperty(propertyName: "has_richsync")]
    public long HasRichsync { get; set; }

    [JsonProperty(propertyName: "has_track_structure")]
    public long HasTrackStructure { get; set; }

    [JsonProperty(propertyName: "num_favourite")]
    public long NumFavourite { get; set; }

    [JsonProperty(propertyName: "lyrics_id")]
    public long LyricsId { get; set; }

    [JsonProperty(propertyName: "subtitle_id")]
    public long SubtitleId { get; set; }

    [JsonProperty(propertyName: "album_id")]
    public long AlbumId { get; set; }

    [JsonProperty(propertyName: "album_name")]
    public string? AlbumName { get; set; }

    [JsonProperty(propertyName: "album_vanity_id")]
    public string AlbumVanityId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "artist_id")]
    public long ArtistId { get; set; }

    [JsonProperty(propertyName: "artist_mbid")]
    public Guid? ArtistMbid { get; set; }

    [JsonProperty(propertyName: "artist_name")]
    public string ArtistName { get; set; } = string.Empty;

    [JsonProperty(propertyName: "album_coverart_100x100")]
    public Uri AlbumCoverart100X100 { get; set; } = null!;

    [JsonProperty(propertyName: "album_coverart_350x350")]
    public Uri AlbumCoverart350X350 { get; set; } = null!;

    [JsonProperty(propertyName: "album_coverart_500x500")]
    public Uri AlbumCoverart500X500 { get; set; } = null!;

    [JsonProperty(propertyName: "album_coverart_800x800")]
    public Uri AlbumCoverart800X800 { get; set; } = null!;

    [JsonProperty(propertyName: "track_share_url")]
    public Uri TrackShareUrl { get; set; } = null!;

    [JsonProperty(propertyName: "track_edit_url")]
    public Uri TrackEditUrl { get; set; } = null!;

    [JsonProperty(propertyName: "commontrack_vanity_id")]
    public string CommontrackVanityId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "restricted")]
    public long Restricted { get; set; }

    [JsonProperty(propertyName: "first-release-date")]
    private string? _firstReleaseDate;

    public DateTime? FirstReleaseDate
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _firstReleaseDate)
            && !string.IsNullOrEmpty(value: _firstReleaseDate)
            && _firstReleaseDate.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _firstReleaseDate = value.ToString().OrEmpty();
    }

    [JsonProperty(propertyName: "updated_time")]
    public DateTimeOffset UpdatedTime { get; set; }

    [JsonProperty(propertyName: "primary_genres")]
    public MusixMatchGenres PrimaryMusixMatchGenres { get; set; } = new();

    [JsonProperty(propertyName: "secondary_genres")]
    public MusixMatchGenres SecondaryMusixMatchGenres { get; set; } = new();
}
