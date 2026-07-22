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

namespace NoMercy.Database.Models.Common;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Key), IsUnique = true)]
public class Configuration : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Identity)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty(propertyName: "value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty(propertyName: "modified_by")]
    public Guid? ModifiedBy { get; set; }

    [JsonIgnore]
    public string? SecureValue { get; set; }
}
