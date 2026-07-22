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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.DTOs;

public class FolderLibraryDto
{
    [JsonProperty(propertyName: "folder_id")]
    public Ulid FolderId { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }

    [JsonProperty(propertyName: "folder")]
    public FolderDto Folder { get; set; } = new();

    public FolderLibraryDto() { }

    public FolderLibraryDto(ICollection<FolderLibrary> folderFolderLibraries)
    {
        if (folderFolderLibraries.Count == 0)
            throw new ArgumentException(
                message: "The collection must contain at least one FolderLibrary.",
                paramName: nameof(folderFolderLibraries)
            );

        FolderId = folderFolderLibraries.First().FolderId;
        LibraryId = folderFolderLibraries.First().LibraryId;
        Folder = new(folder: folderFolderLibraries.First().Folder);
    }
}
