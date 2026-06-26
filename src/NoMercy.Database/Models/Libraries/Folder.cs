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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Infrastructure;
using NoMercy.Database.Models.Storage;

// EncodingPresetFolder is referenced via [InverseProperty]

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(nameof(Id))]
[Index(nameof(DriverId), nameof(Path), IsUnique = true)]
public class Folder
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("path")]
    public string Path
    {
        get;
        set => field = PathNormalizer.Normalize(value);
    } = string.Empty;

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }

    [JsonProperty("driver")]
    public Driver? Driver { get; set; }

    [JsonProperty("encoder_profile_folder")]
    public ICollection<EncoderProfileFolder> EncoderProfileFolder { get; set; } = [];

    [JsonProperty("encoding_preset_folders")]
    [InverseProperty(nameof(EncodingPresetFolder.Folder))]
    public ICollection<EncodingPresetFolder> EncodingPresetFolders { get; set; } = [];

    [JsonProperty("folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = [];
}
