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

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(DriverId), additionalPropertyNames: nameof(Path), IsUnique = true)]
public class Folder
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "path")]
    public string Path
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "driver_id")]
    public Ulid DriverId { get; set; }

    [JsonProperty(propertyName: "driver")]
    public Driver? Driver { get; set; }

    [JsonProperty(propertyName: "encoding_preset_folders")]
    [InverseProperty(property: nameof(EncodingPresetFolder.Folder))]
    public ICollection<EncodingPresetFolder> EncodingPresetFolders { get; set; } = [];

    [JsonProperty(propertyName: "folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = [];
}
