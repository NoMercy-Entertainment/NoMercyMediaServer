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

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(nameof(FolderId), nameof(LibraryId))]
[Index(nameof(FolderId))]
[Index(nameof(LibraryId))]
public class FolderLibrary
{
    [JsonProperty("folder_id")]
    public Ulid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    [JsonProperty("library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public FolderLibrary(Ulid folderId, Ulid libraryId)
    {
        FolderId = folderId;
        LibraryId = libraryId;
    }

    public FolderLibrary() { }
}
