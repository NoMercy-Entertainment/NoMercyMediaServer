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

namespace NoMercy.Providers.MusicBrainz.Models;

public class AttributeCredits
{
    [JsonProperty(propertyName: "Rhodes piano")]
    public string RhodesPiano { get; set; } = string.Empty;

    [JsonProperty(propertyName: "synthesizer")]
    public string Synthesizer { get; set; } = string.Empty;

    [JsonProperty(propertyName: "drums (drum set)")]
    public string? DrumsDrumSet { get; set; }

    [JsonProperty(propertyName: "handclaps")]
    public string Handclaps { get; set; } = string.Empty;

    [JsonProperty(propertyName: "Hammond organ")]
    public string HammondOrgan { get; set; } = string.Empty;

    [JsonProperty(propertyName: "keyboard")]
    public string Keyboard { get; set; } = string.Empty;

    [JsonProperty(propertyName: "drum machine")]
    public string DrumMachine { get; set; } = string.Empty;

    [JsonProperty(propertyName: "foot stomps")]
    public string FootStomps { get; set; } = string.Empty;

    [JsonProperty(propertyName: "Wurlitzer electric piano")]
    public string WurlitzerElectricPiano { get; set; } = string.Empty;
}
