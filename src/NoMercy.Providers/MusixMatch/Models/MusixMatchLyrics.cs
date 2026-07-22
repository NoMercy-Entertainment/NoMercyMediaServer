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

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchLyrics
{
    [JsonProperty(propertyName: "lyrics_id")]
    public long LyricsId { get; set; }

    [JsonProperty(propertyName: "can_edit")]
    public long CanEdit { get; set; }

    [JsonProperty(propertyName: "check_validation_overridable")]
    public long CheckValidationOverridable { get; set; }

    [JsonProperty(propertyName: "locked")]
    public long Locked { get; set; }

    [JsonProperty(propertyName: "published_status")]
    public long PublishedStatus { get; set; }

    [JsonProperty(propertyName: "action_requested")]
    public string ActionRequested { get; set; } = string.Empty;

    [JsonProperty(propertyName: "verified")]
    public long Verified { get; set; }

    [JsonProperty(propertyName: "restricted")]
    public long Restricted { get; set; }

    [JsonProperty(propertyName: "instrumental")]
    public long Instrumental { get; set; }

    [JsonProperty(propertyName: "explicit")]
    public long Explicit { get; set; }

    [JsonProperty(propertyName: "lyrics_body")]
    public string LyricsBody { get; set; } = string.Empty;

    [JsonProperty(propertyName: "lyrics_language")]
    public string LyricsLanguage { get; set; } = string.Empty;

    [JsonProperty(propertyName: "lyrics_language_description")]
    public string LyricsLanguageDescription { get; set; } = string.Empty;

    [JsonProperty(propertyName: "script_tracking_url")]
    public Uri ScriptTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "pixel_tracking_url")]
    public Uri PixelTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "html_tracking_url")]
    public Uri HtmlTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "lyrics_copyright")]
    public string LyricsCopyright { get; set; } = string.Empty;

    [JsonProperty(propertyName: "writer_list")]
    public object[] WriterList { get; set; } = [];

    [JsonProperty(propertyName: "publisher_list")]
    public object[] PublisherList { get; set; } = [];

    [JsonProperty(propertyName: "backlink_url")]
    public Uri BacklinkUrl { get; set; } = null!;

    [JsonProperty(propertyName: "updated_time")]
    public DateTimeOffset UpdatedTime { get; set; }
}
