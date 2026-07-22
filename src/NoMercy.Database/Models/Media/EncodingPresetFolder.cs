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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(PresetId), additionalPropertyNames: nameof(FolderId))]
[Index(propertyName: nameof(FolderId))]
[Index(propertyName: nameof(PresetId), additionalPropertyNames: nameof(IsDefault))]
public class EncodingPresetFolder
{
    [JsonProperty(propertyName: "preset_id")]
    public Ulid PresetId { get; set; }

    [JsonProperty(propertyName: "folder_id")]
    public Ulid FolderId { get; set; }

    [JsonProperty(propertyName: "is_default")]
    public bool IsDefault { get; set; }

    public EncodingPreset? Preset { get; set; }
    public Folder? Folder { get; set; }
}
