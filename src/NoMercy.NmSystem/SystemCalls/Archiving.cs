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

using System.IO.Compression;
using NoMercy.NmSystem.FileSystem;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public static class Archiving
{
    public static async Task<List<string>> ExtractArchive(
        IStorage storage,
        string filePath,
        string destination
    )
    {
        List<string> extractedFiles;

        if (filePath.EndsWith(".zip"))
        {
            extractedFiles = ExtractZipFile(storage, filePath, destination);
        }
        else if (
            filePath.EndsWith(".tar.xz")
            || filePath.EndsWith(".tar.gz")
            || filePath.EndsWith(".tgz")
        )
        {
            extractedFiles = await ExtractTarFile(storage, filePath, destination);
        }
        else
        {
            Logger.System($"Unsupported archive format for {filePath}", LogEventLevel.Error);
            return [];
        }

        foreach (string extractedFile in extractedFiles)
            await FilePermissions.SetExecutionPermissions(extractedFile);

        return extractedFiles;
    }

    private static List<string> ExtractZipFile(
        IStorage storage,
        string zipFilePath,
        string extractToDirectory
    )
    {
        List<string> extractedFiles = [];
        string destinationRoot = Path.GetFullPath(extractToDirectory);

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractToDirectory, entry.FullName);

                if (!IsPathContained(destinationRoot, destinationPath))
                {
                    Logger.System(
                        $"Rejected zip-slip entry '{entry.FullName}' in {zipFilePath}: resolves outside {destinationRoot}",
                        LogEventLevel.Error
                    );
                    throw new InvalidDataException(
                        $"Archive entry '{entry.FullName}' escapes the extraction root."
                    );
                }

                string destinationDir =
                    Path.GetDirectoryName(destinationPath) ?? extractToDirectory;

                if (!storage.Exists(destinationDir))
                    storage.CreateDirectory(destinationDir);

                if (string.IsNullOrEmpty(entry.Name)) // Skip directories
                    continue;

                entry.ExtractToFile(destinationPath, true);

                extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract zip file {zipFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract zip file {zipFilePath}", ex);
        }

        return extractedFiles;
    }

    private static async Task<List<string>> ExtractTarFile(
        IStorage storage,
        string tarFilePath,
        string extractToDirectory
    )
    {
        List<string> extractedFiles = [];
        string destinationRoot = Path.GetFullPath(extractToDirectory);

        try
        {
            // List entries first so a traversal attempt can be rejected before
            // any file is written — the tar CLI has no per-entry containment guard.
            Shell.ExecResult listResult = await Shell.ExecAsync(
                "tar",
                $"tf \"{tarFilePath}\""
            );

            if (listResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to list tar file {tarFilePath}: {listResult.StandardError}"
                );
            }

            string[] entries = listResult.StandardOutput.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries
            );

            List<string> destinationPaths = [];
            foreach (string line in entries)
            {
                string entryName = line.Trim();
                string destinationPath = Path.Combine(extractToDirectory, entryName);

                if (!IsPathContained(destinationRoot, destinationPath))
                {
                    Logger.System(
                        $"Rejected zip-slip entry '{entryName}' in {tarFilePath}: resolves outside {destinationRoot}",
                        LogEventLevel.Error
                    );
                    throw new InvalidDataException(
                        $"Archive entry '{entryName}' escapes the extraction root."
                    );
                }

                destinationPaths.Add(destinationPath);
            }

            Shell.ExecResult extractResult = await Shell.ExecAsync(
                "tar",
                $"xf \"{tarFilePath}\" -C \"{extractToDirectory}\""
            );

            if (extractResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"tar exited with code {extractResult.ExitCode} extracting {tarFilePath}: {extractResult.StandardError}"
                );
            }

            foreach (string destinationPath in destinationPaths)
                if (storage.Exists(destinationPath))
                    extractedFiles.Add(destinationPath);
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract tar file {tarFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract tar file {tarFilePath}", ex);
        }

        return extractedFiles;
    }

    /// <summary>
    /// Resolves both paths to their canonical full form and rejects any
    /// candidate that does not fall inside the destination root — the
    /// zip-slip / tar-slip guard against archive entries such as
    /// <c>../../evil</c> or an absolute path.
    /// </summary>
    private static bool IsPathContained(string destinationRoot, string candidatePath)
    {
        string normalizedRoot = destinationRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        string fullCandidatePath = Path.GetFullPath(candidatePath);

        return fullCandidatePath == normalizedRoot
            || fullCandidatePath.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            );
    }
}
