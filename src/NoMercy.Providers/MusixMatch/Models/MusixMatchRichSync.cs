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
    [JsonProperty(propertyName: "richsync_id")]
    public int RichsyncId;

    [JsonProperty(propertyName: "restricted")]
    public int Restricted;

    [JsonProperty(propertyName: "richsync_body")]
    public string RichsyncBody = string.Empty;

    [JsonProperty(propertyName: "lyrics_copyright")]
    public string LyricsCopyright = string.Empty;

    [JsonProperty(propertyName: "richsync_length")]
    public int RichsyncLength;

    [JsonProperty(propertyName: "richsync_language")]
    public string RichsyncLanguage = string.Empty;

    [JsonProperty(propertyName: "richsync_language_description")]
    public string RichsyncLanguageDescription = string.Empty;

    [JsonProperty(propertyName: "script_tracking_url")]
    public string ScriptTrackingUrl = string.Empty;

    [JsonProperty(propertyName: "updated_time")]
    public DateTime UpdatedTime;
}
