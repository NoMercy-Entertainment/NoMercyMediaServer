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

namespace NoMercy.NmSystem.FileSystem;

public static class Folders
{
    public static void EmptyFolder(string folderPath)
    {
        DirectoryInfo directory = new(path: folderPath);
        if (!directory.Exists)
            return;

        foreach (FileInfo file in directory.GetFiles())
            file.Delete();

        foreach (DirectoryInfo subDirectory in directory.GetDirectories())
            subDirectory.Delete(recursive: true);
    }

    public static long GetDirectorySize(this DirectoryInfo directoryInfo, bool recursive = true)
    {
        long startDirectorySize = 0;
        if (!directoryInfo.Exists)
            return startDirectorySize;

        try
        {
            foreach (FileInfo fileInfo in directoryInfo.GetFiles())
                Interlocked.Add(location1: ref startDirectorySize, value: fileInfo.Length);

            if (recursive)
                Parallel.ForEach(
                    source: directoryInfo.GetDirectories(),
                    parallelOptions: SystemParallelism.Options,
                    body: subDirectory =>
                        Interlocked.Add(
                            location1: ref startDirectorySize,
                            value: subDirectory.GetDirectorySize(recursive: recursive)
                        )
                );

            return startDirectorySize;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
