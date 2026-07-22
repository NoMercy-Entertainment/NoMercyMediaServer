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

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models.Encoder;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(MediaId), additionalPropertyNames: nameof(FolderId), IsUnique = true)]
public class IncompleteEncode
{
    public int Id { get; set; }

    public required long MediaId { get; set; }

    [MaxLength(length: 256)]
    public required string FolderId { get; set; }

    [MaxLength(length: 512)]
    public required string Title { get; set; }

    [MaxLength(length: int.MaxValue)]
    public required string MissingRenditions { get; set; }

    [MaxLength(length: 4096)]
    public string? LastError { get; set; }

    public required int AttemptsMade { get; set; }

    public required DateTime FirstSeenAt { get; set; }

    public required DateTime LastSeenAt { get; set; }
}
