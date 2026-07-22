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

using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Files;

public class FileWatcherEventArgs
{
    // ReSharper disable once MemberCanBePrivate.Global
    public FileSystemEventArgs FileSystemEventArgs { get; private set; }
    public ErrorEventArgs? ErrorEventArgs { get; set; }
    public WatcherChangeTypes ChangeType => FileSystemEventArgs.ChangeType;

    public string Root { get; set; }
    public string Path { get; set; }
    public string FullPath { get; set; }
    public string? OldFullPath { get; set; }
    public FileSystemWatcher? Sender { get; set; }

    public FileWatcherEventArgs(FileSystemWatcher? sender, FileSystemEventArgs fileSystemEventArgs)
    {
        FileSystemEventArgs = fileSystemEventArgs;
        Sender = sender;
        Root = (sender?.Path).OrEmpty();
        Path = System.IO.Path.GetDirectoryName(path: fileSystemEventArgs.FullPath).OrEmpty();
        FullPath = fileSystemEventArgs.FullPath;
        OldFullPath = (fileSystemEventArgs as RenamedEventArgs)?.OldFullPath;
    }
}
