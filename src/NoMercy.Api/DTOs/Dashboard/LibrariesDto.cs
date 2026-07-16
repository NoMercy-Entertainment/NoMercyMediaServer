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
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public record LibrariesDto
{
    [JsonProperty("data")]
    public IEnumerable<LibrariesResponseItemDto> Data { get; set; } = [];

    public static readonly Func<MediaContext, Guid, IAsyncEnumerable<Library?>> GetLibraries =
        EF.CompileAsyncQuery(
            (MediaContext mediaContext, Guid userId) =>
                mediaContext
                    .Libraries.AsNoTracking()
                    .Where(library =>
                        library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null
                    )
                    .Include(library => library.FolderLibraries)
                        .ThenInclude(folderLibrary => folderLibrary.Folder)
                            .ThenInclude(folder => folder.EncodingPresetFolders)
                                .ThenInclude(link => link.Preset)
                    .Include(library => library.LanguageLibraries)
                        .ThenInclude(languageLibrary => languageLibrary.Language)
        );
}
