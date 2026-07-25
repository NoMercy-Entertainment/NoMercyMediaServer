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

public class MusixMatchRichSync
{
    [JsonProperty("richsync_id")]
    public int RichsyncId;

    [JsonProperty("restricted")]
    public int Restricted;

    [JsonProperty("richsync_body")]
    public string RichsyncBody = string.Empty;

    [JsonProperty("lyrics_copyright")]
    public string LyricsCopyright = string.Empty;

    [JsonProperty("richsync_length")]
    public int RichsyncLength;

    [JsonProperty("richsync_language")]
    public string RichsyncLanguage = string.Empty;

    [JsonProperty("richsync_language_description")]
    public string RichsyncLanguageDescription = string.Empty;

    [JsonProperty("script_tracking_url")]
    public string ScriptTrackingUrl = string.Empty;

    [JsonProperty("updated_time")]
    public DateTime UpdatedTime;
}
