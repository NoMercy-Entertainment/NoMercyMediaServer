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
using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchSubtitle
{
    [JsonProperty(propertyName: "subtitle_id")]
    public long SubtitleId { get; set; }

    [JsonProperty(propertyName: "restricted")]
    public long Restricted { get; set; }

    [JsonProperty(propertyName: "published_status")]
    public long PublishedStatus { get; set; }

    [Column(name: "SubtitleBody")]
    [JsonProperty(propertyName: "subtitle_body")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitle_body { get; set; }

    [NotMapped]
    public MusixMatchFormattedLyric[]? SubtitleBody
    {
        get =>
            _subtitle_body is null
                ? null
                : JsonConvert.DeserializeObject<MusixMatchFormattedLyric[]>(value: _subtitle_body);
        set => _subtitle_body = JsonConvert.SerializeObject(value: value);
    }

    [JsonProperty(propertyName: "subtitle_avg_count")]
    public long SubtitleAvgCount { get; set; }

    [JsonProperty(propertyName: "lyrics_copyright")]
    public string LyricsCopyright { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitle_length")]
    public long SubtitleLength { get; set; }

    [JsonProperty(propertyName: "subtitle_language")]
    public string SubtitleLanguage { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitle_language_description")]
    public string SubtitleLanguageDescription { get; set; } = string.Empty;

    [JsonProperty(propertyName: "script_tracking_url")]
    public Uri ScriptTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "pixel_tracking_url")]
    public Uri PixelTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "html_tracking_url")]
    public Uri HtmlTrackingUrl { get; set; } = null!;

    [JsonProperty(propertyName: "writer_list")]
    public object[] WriterList { get; set; } = [];

    [JsonProperty(propertyName: "publisher_list")]
    public object[] PublisherList { get; set; } = [];

    [JsonProperty(propertyName: "updated_time")]
    public DateTimeOffset UpdatedTime { get; set; }
}
