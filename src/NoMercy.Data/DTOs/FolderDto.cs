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
using NoMercy.Data.DTOs.Encoder;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.DTOs;

public class FolderDto
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }

    [JsonProperty("driver_name")]
    public string DriverName { get; set; } = string.Empty;

    [JsonProperty("encoder_profiles")]
    public FolderPresetDto[] EncoderProfiles { get; set; } = [];

    public FolderDto() { }

    public FolderDto(Folder folder)
    {
        Id = folder.Id;
        Path = folder.Path;
        DriverId = folder.DriverId;
        DriverName = folder.Driver?.Name ?? string.Empty;
        EncoderProfiles = folder
            .EncodingPresetFolders.Where(link => link.Preset is not null)
            .Select(link => new FolderPresetDto
            {
                Id = link.Preset!.Id,
                Name = link.Preset!.Name,
                // The preset row carries no Container column (that was a V1-only
                // concept resolved from ProfileJson at encode time) — resolving it
                // here would mean deserializing ProfileJson per folder per request,
                // which isn't cheap enough to justify for a field clients never read.
                Container = string.Empty,
            })
            .ToArray();
    }
}
