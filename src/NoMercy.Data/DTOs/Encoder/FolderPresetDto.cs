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

namespace NoMercy.Data.DTOs.Encoder;

/// <summary>
/// One entry of <see cref="FolderDto.EncoderProfiles"/> — the wire shape a
/// folder's linked encoding preset serializes as under the JSON key
/// <c>encoder_profiles</c>. Kept intentionally sparse: only <see cref="Id"/>,
/// <see cref="Name"/> and <see cref="Container"/> are ever populated by the
/// preset system, but <see cref="Params"/> and <see cref="EncoderProfileFolder"/>
/// stay on the shape (always their default value) so existing self-hosted
/// clients parsing the historical <c>EncoderProfileDto</c> shape keep working
/// unchanged.
/// </summary>
public class FolderPresetDto
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("container")]
    public string Container { get; set; } = string.Empty;

    [JsonProperty("params")]
    public FolderPresetParamsDto Params { get; set; } = new();

    [JsonProperty("encoder_profile_folder")]
    public List<object> EncoderProfileFolder { get; set; } = [];
}

/// <summary>
/// Always-default sub-shape preserved for wire compatibility — see
/// <see cref="FolderPresetDto"/>.
/// </summary>
public class FolderPresetParamsDto
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("crf")]
    public int Crf { get; set; }

    [JsonProperty("preset")]
    public string Preset { get; set; } = string.Empty;

    [JsonProperty("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonProperty("codec")]
    public string Codec { get; set; } = string.Empty;

    [JsonProperty("audio")]
    public string Audio { get; set; } = string.Empty;
}
