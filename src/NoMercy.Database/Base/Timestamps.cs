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

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Database;

public class Timestamps
{
    [DefaultValue(value: "CURRENT_TIMESTAMP")]
    [JsonProperty(propertyName: "created_at")]
    [TypeConverter(typeName: "TIMESTAMP")]
    [Timestamp]
    [Required]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Computed)]
    public DateTime CreatedAt { get; set; }

    [DefaultValue(value: "CURRENT_TIMESTAMP")]
    [JsonProperty(propertyName: "updated_at")]
    [TypeConverter(typeName: "TIMESTAMP")]
    [Timestamp]
    [Required]
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }
}
