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

[PrimaryKey(nameof(EncoderProfileId), nameof(FolderId))]
[Index(nameof(EncoderProfileId))]
[Index(nameof(FolderId))]
public class EncoderProfileFolder
{
    [JsonProperty("encoder_profile_id")]
    public Ulid EncoderProfileId { get; set; }
    public EncoderProfile EncoderProfile { get; set; } = null!;

    [JsonProperty("folder_id")]
    public Ulid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    public EncoderProfileFolder() { }

    public EncoderProfileFolder(Ulid encoderProfileId, Ulid libraryId)
    {
        EncoderProfileId = encoderProfileId;
        FolderId = libraryId;
    }
}
