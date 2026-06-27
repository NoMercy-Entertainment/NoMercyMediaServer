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
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.DTOs.Encoder;

public class EncoderProfileDto
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("container")]
    public string Container { get; set; } = string.Empty;

    [JsonProperty("params")]
    public EncoderProfileParamsDto Params { get; set; } = new();

    [JsonProperty("encoder_profile_folder")]
    public List<EncoderProfileFolderDto> EncoderProfileFolder { get; set; } = [];

    public EncoderProfileDto() { }

    public EncoderProfileDto(EncoderProfile argEncoderProfile)
    {
        Id = argEncoderProfile.Id;
        Name = argEncoderProfile.Name;
        Container = argEncoderProfile.Container.OrEmpty();
        Params = new(argEncoderProfile);
        EncoderProfileFolder = argEncoderProfile
            .EncoderProfileFolder.Select(ef => new EncoderProfileFolderDto(ef))
            .ToList();
    }
}
