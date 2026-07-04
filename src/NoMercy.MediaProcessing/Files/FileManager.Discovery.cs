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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager
{
    private async Task MediaType(int id, Library library)
    {
        (Movie, Show, Type) = await fileRepository.MediaType(id, library);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Library library, Folder folder)
    {
        // Mount at configured root; MediaScan walks via absolute paths.
        IStorage folderStorage = storageFactory.For(folder.Id, folder.DriverId, string.Empty);
        string scanRoot = folderStorage.GetFullPath(folder.Path);
        MediaScan mediaScan = new(folderStorage.Driver);

        int depth = library.Type switch
        {
            MediaTypes.MovieMediaType => 1,
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => 2,
            _ => 0,
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .FilterByMediaType(library.Type)
            .FilterByFileName(Filter)
            .Process(scanRoot, depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    private List<Folder> Paths(Library library, Movie? movie = null, Tv? show = null)
    {
        List<Folder> folders = [];
        string? folder = library.Type switch
        {
            MediaTypes.MovieMediaType => movie?.Folder?.Replace("/", ""),
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => show?.Folder?.Replace("/", ""),
            _ => "",
        };

        if (folder == null)
        {
            Logger.App("Folder not set");
            return folders;
        }

        using MediaContext mediaContext = new();

        // Scope to the target library's folders only. The previous query
        // grabbed every FolderLibrary row system-wide, so a rescan of a movie
        // in an NFS library would also probe every S3 / WebDAV folder for
        // unrelated libraries — one flaky remote backend then threw on
        // Exists() and killed the whole job (retried up to maxAttempts).
        Folder[] rootFolders = mediaContext
            .FolderLibrary.Where(fl => fl.LibraryId == library.Id)
            .Include(fl => fl.Folder)
                .ThenInclude(fl => fl.Driver)
            .Select(f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            IStorage folderStorage = StorageFor(rootFolder);
            string resolvedRoot = folderStorage.GetFullPath(rootFolder.Path);
            string path = folderStorage.CombinePath(resolvedRoot, folder);

            // Treat a transport-level failure from any single backend as
            // "not in this folder" rather than aborting the whole rescan. The
            // job is idempotent on its successful folders, and one transient
            // S3 502 should not trigger queue-level retries.
            bool exists = TryExists(folderStorage, path);

            if (!exists)
            {
                string? match = TryFindMatchingDirectory(
                    folderStorage.Driver,
                    resolvedRoot,
                    folder
                );
                if (match != null)
                {
                    path = match;
                    exists = TryExists(folderStorage, path);
                }
            }

            if (exists)
                folders.Add(
                    new()
                    {
                        Path = path,
                        Id = rootFolder.Id,
                        DriverId = rootFolder.DriverId,
                        Driver = rootFolder.Driver,
                    }
                );
        }

        return folders;
    }

    private static bool TryExists(IStorage storage, string path)
    {
        try
        {
            return storage.Exists(path);
        }
        catch (Exception ex)
        {
            Logger.App(
                $"[FileManager.Paths] storage.Exists threw for '{path}' on driver {storage.Driver.GetType().Name}: {ex.Message}",
                LogEventLevel.Warning
            );
            return false;
        }
    }

    private static string? TryFindMatchingDirectory(
        IStorageDriver driver,
        string rootPath,
        string expectedFolderName
    )
    {
        try
        {
            return FileNameSanitizer.FindMatchingDirectory(driver, rootPath, expectedFolderName);
        }
        catch (Exception ex)
        {
            Logger.App(
                $"[FileManager.Paths] FindMatchingDirectory threw for root '{rootPath}' on driver {driver.GetType().Name}: {ex.Message}",
                LogEventLevel.Warning
            );
            return null;
        }
    }

    private static string ComputeFileHash(IStorage storage, string filePath)
    {
        using SHA256 sha256 = SHA256.Create();
        using Stream fileStream = storage.OpenRead(filePath);

        byte[] hashBytes = sha256.ComputeHash(fileStream);

        StringBuilder hashStringBuilder = new(64);

        foreach (byte b in hashBytes)
            hashStringBuilder.Append(b.ToString("x2"));
        return hashStringBuilder.ToString();
    }

    // Liberal subtitle filename matcher: 2-3 char language (ISO 639-1 or -3), arbitrary
    // type (alt, full, sign, song, sdh, forced, commentary, director, ...), 3-6 char
    // extension (vtt, ass, srt, ssa, sub, idx, sup, vob, webvtt).  Previously the regex
    // only matched 3-char lang + 3-4 char type + 3-char ext and the consumer methods
    // further filtered to type in (sign, song, full) — both combined silently dropped
    // every "alt" / "sdh" / "forced" subtitle file.
    [GeneratedRegex(@"(?<lang>[a-zA-Z]{2,3})\.(?<type>\w+)\.(?<ext>\w{3,6})$")]
    private static partial Regex SubtitleFileRegex();

    [GeneratedRegex(@"#xywh=\d+,\d+,(?<width>\d+),(?<height>\d+)")]
    private static partial Regex ImageDimensions();

    [GeneratedRegex(@"^video_(?<width>\d+)x(?<height>\d+)(?:_(?:SDR|HDR))?$")]
    private static partial Regex VideoDirectoryRegex();

    [GeneratedRegex(@"^audio_(?<lang>\w+)_(?<codec>\w+)$")]
    private static partial Regex AudioDirectoryRegex();
}
