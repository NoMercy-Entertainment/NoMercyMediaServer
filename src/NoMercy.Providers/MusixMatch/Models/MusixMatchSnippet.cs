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

public class MusixMatchSnippet
{
    [JsonProperty("snippet_id")]
    public long SnippetId { get; set; }

    [JsonProperty("snippet_language")]
    public string SnippetLanguage { get; set; } = string.Empty;

    [JsonProperty("restricted")]
    public long Restricted { get; set; }

    [JsonProperty("instrumental")]
    public long Instrumental { get; set; }

    [JsonProperty("snippet_body")]
    public string SnippetBody { get; set; } = string.Empty;

    [JsonProperty("script_tracking_url")]
    public Uri ScriptTrackingUrl { get; set; } = null!;

    [JsonProperty("pixel_tracking_url")]
    public Uri PixelTrackingUrl { get; set; } = null!;

    [JsonProperty("html_tracking_url")]
    public Uri HtmlTrackingUrl { get; set; } = null!;

    [JsonProperty("updated_time")]
    public DateTimeOffset UpdatedTime { get; set; }
}
