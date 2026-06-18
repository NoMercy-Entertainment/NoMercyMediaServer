// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models.Encoder;

[PrimaryKey(nameof(Id))]
[Index(nameof(MediaId), nameof(FolderId), IsUnique = true)]
public class IncompleteEncode
{
    public int Id { get; set; }

    public required long MediaId { get; set; }

    [MaxLength(256)]
    public required string FolderId { get; set; }

    [MaxLength(512)]
    public required string Title { get; set; }

    [MaxLength(int.MaxValue)]
    public required string MissingRenditions { get; set; }

    [MaxLength(4096)]
    public string? LastError { get; set; }

    public required int AttemptsMade { get; set; }

    public required DateTime FirstSeenAt { get; set; }

    public required DateTime LastSeenAt { get; set; }
}
