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
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Logic;

public class EncoderProfileParamsDto
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

    public EncoderProfileParamsDto() { }

    public EncoderProfileParamsDto(EncoderProfile argEncoderProfile)
    {
        VideoProfile[] videoProfiles = argEncoderProfile.VideoProfiles;
        if (videoProfiles.Length > 0)
        {
            VideoProfile video = videoProfiles[0];
            Width = video.Width;
            Crf = video.Crf;
            Preset = video.Preset;
            Profile = video.Profile;
            Codec = video.Codec;
        }

        AudioProfile[] audioProfiles = argEncoderProfile.AudioProfiles;
        if (audioProfiles.Length > 0)
            Audio = audioProfiles[0].Codec;
    }
}
