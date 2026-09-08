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

namespace NoMercy.Database;

public class VideoTrack
{
    [JsonProperty("file")]
    public string File { get; set; } = null!;

    [JsonProperty("kind")]
    public string Kind { get; set; } = null!;

    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public string? Language { get; set; }

    /// <summary>
    /// The subtitle's container — vtt, ass, sup. Clients default a missing one
    /// to vtt, so every variant of a language arrived labelled VTT and the menu
    /// showed the same row two and three times over: one file per format sits on
    /// disk (…eng.full.ass, .sup, .vtt) and the payload described them all as
    /// the same thing. Omitted for non-subtitle tracks, which have no container
    /// to state.
    /// </summary>
    [JsonProperty("ext", NullValueHandling = NullValueHandling.Ignore)]
    public string? Ext { get; set; }
}
