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
using NoMercy.Data.Logic;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Extensions;

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
    public EncoderProfileDto[] EncoderProfiles { get; set; } = [];

    public FolderDto() { }

    public FolderDto(Folder folder)
    {
        Id = folder.Id;
        Path = folder.Path;
        DriverId = folder.DriverId;
        DriverName = folder.Driver?.Name ?? string.Empty;
        EncoderProfiles = folder
            .EncoderProfileFolder.Where(f => f.EncoderProfile is not null)
            .Select(f => new EncoderProfileDto
            {
                Id = f.EncoderProfile.Id,
                Name = f.EncoderProfile.Name,
                Container = f.EncoderProfile.Container.OrEmpty(),
            })
            .ToArray();
    }
}
