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

using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Files;

public class FileChangeGroup(WatcherChangeTypes type, Library library, string folderPath)
{
    public string FolderPath { get; set; } = folderPath;
    public string? FullPath { get; set; }
    public string? OldFullPath { get; set; }
    public Library Library { get; set; } = library;
    public WatcherChangeTypes ChangeType { get; set; } = type;
    public Timer? Timer { get; set; }
}
