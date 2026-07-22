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
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager
{
    private async Task MediaType(int id, Library library)
    {
        (Movie, Show, Type) = await fileRepository.MediaType(id: id, library: library);
    }

    private async Task<ConcurrentBag<MediaFolderExtend>> GetFiles(Library library, Folder folder)
    {
        IStorage folderStorage = storageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: string.Empty);
        string scanRoot = ResolveBackendPath(storage: folderStorage, scopeRelativePath: folder.Path);
        MediaScan mediaScan = new(driver: folderStorage.Driver);

        int depth = library.Type switch
        {
            MediaTypes.MovieMediaType => 1,
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => 2,
            _ => 0,
        };

        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .FilterByMediaType(mediaType: library.Type)
            .FilterByFileName(filter: Filter)
            .Process(rootFolder: scanRoot, depth: depth);

        await mediaScan.DisposeAsync();

        return folders;
    }

    // Resolve a scope-relative folder key ("Anime/Anime/Show") to the backend-
    // absolute path the raw driver's file APIs (EnumerateFileSystemEntries,
    // DirectoryExists, MediaScan) expect.
    //
    // A local library's root lives in the LocalStorage facade's path guard, not
    // in the stateless LocalStorageDriver — its GetFullPath is a bare
    // Path.GetFullPath that canonicalizes against the process CWD (/app in the
    // container). Resolving a scope-relative key through the driver therefore
    // produced "/app/Anime/Anime/Show" instead of the library root, and every
    // local-library rescan / move silently found zero files. Remote backends
    // (NFS / S3 / SMB / WebDAV) carry their own export/bucket root inside the
    // driver and don't implement the facade's local-only GetFullPath escape
    // hatch, so they must resolve through the driver.
    private static string ResolveBackendPath(IStorage storage, string scopeRelativePath) =>
        storage.Driver is LocalStorageDriver
            ? storage.GetFullPath(path: scopeRelativePath)
            : storage.Driver.GetFullPath(path: scopeRelativePath);

    private List<Folder> Paths(Library library, Movie? movie = null, Tv? show = null)
    {
        List<Folder> folders = [];
        string? folder = library.Type switch
        {
            MediaTypes.MovieMediaType => movie?.Folder?.Replace(oldValue: "/", newValue: ""),
            MediaTypes.TvMediaType or MediaTypes.AnimeMediaType => show?.Folder?.Replace(oldValue: "/", newValue: ""),
            _ => "",
        };

        if (folder == null)
        {
            Logger.App(message: "Folder not set");
            return folders;
        }

        using MediaContext mediaContext = new();

        // Scope to the target library's folders only. The previous query
        // grabbed every FolderLibrary row system-wide, so a rescan of a movie
        // in an NFS library would also probe every S3 / WebDAV folder for
        // unrelated libraries — one flaky remote backend then threw on
        // Exists() and killed the whole job (retried up to maxAttempts).
        Folder[] rootFolders = mediaContext
            .FolderLibrary.Where(predicate: fl => fl.LibraryId == library.Id)
            .Include(navigationPropertyPath: fl => fl.Folder)
                .ThenInclude(navigationPropertyPath: fl => fl.Driver)
            .Select(selector: f => f.Folder)
            .ToArray();

        foreach (Folder rootFolder in rootFolders)
        {
            IStorage folderStorage = StorageFor(folder: rootFolder);

            // Stay in scope-relative space: rootFolder.Path is the storage key
            // as the facade wants it (e.g. "Marvels/TV.Shows"), and the facade
            // Exists / List / downstream scan all reject absolute paths. Do NOT
            // resolve to an OS/mount-absolute path here — GetFullPath (facade
            // OR driver) produces "/mnt/vault/..." which StoragePathGuard then
            // rejects as a scope-relative key.
            string path = folderStorage.CombinePath(parent: rootFolder.Path, child: folder);

            // Treat a transport-level failure from any single backend as
            // "not in this folder" rather than aborting the whole rescan. The
            // job is idempotent on its successful folders, and one transient
            // S3 502 should not trigger queue-level retries.
            bool exists = TryExists(storage: folderStorage, path: path);

            if (!exists)
            {
                // FindMatchingDirectory walks the raw driver (DirectoryExists /
                // EnumerateFileSystemEntries), so it needs the driver-absolute
                // root and returns a driver-absolute directory. Convert that hit
                // back to a scope-relative key before storing / re-checking, so
                // the facade and the downstream scan keep working in one space.
                string resolvedRoot = ResolveBackendPath(storage: folderStorage, scopeRelativePath: rootFolder.Path);
                string? match = TryFindMatchingDirectory(
                    driver: folderStorage.Driver,
                    rootPath: resolvedRoot,
                    expectedFolderName: folder
                );
                if (match != null)
                {
                    path = folderStorage.CombinePath(parent: rootFolder.Path, child: folderStorage.GetName(path: match));
                    exists = TryExists(storage: folderStorage, path: path);
                }
            }

            if (exists)
                folders.Add(
                    item: new()
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
            return storage.Exists(path: path);
        }
        catch (Exception ex)
        {
            Logger.App(
                message: $"[FileManager.Paths] storage.Exists threw for '{path}' on driver {storage.Driver.GetType().Name}: {ex.Message}",
                level: LogEventLevel.Warning
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
            return FileNameSanitizer.FindMatchingDirectory(driver: driver, rootPath: rootPath, expectedFolderName: expectedFolderName);
        }
        catch (Exception ex)
        {
            Logger.App(
                message: $"[FileManager.Paths] FindMatchingDirectory threw for root '{rootPath}' on driver {driver.GetType().Name}: {ex.Message}",
                level: LogEventLevel.Warning
            );
            return null;
        }
    }

    // A scan verifies completeness, not integrity, so it must not stream every
    // file's bytes over the network to hash them — that turned each rescan into a
    // full re-read of every playlist, subtitle, font, and (multi-megabyte) preview
    // sprite. Fingerprint the file by size + last-write time instead: two cheap
    // stats, no content read. The value still changes whenever the file changes,
    // which is all any consumer needs (only fonts.FileHash is read downstream, for
    // cache-busting), while a several-second sprite read collapses to a stat.
    private static string ComputeFileHash(IStorage storage, string filePath)
    {
        long size = storage.SizeOrZero(path: filePath);

        long modifiedTicks;
        try
        {
            modifiedTicks = storage.LastModified(path: filePath).UtcTicks;
        }
        catch
        {
            modifiedTicks = 0;
        }

        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: $"{size}:{modifiedTicks}"));

        StringBuilder hashStringBuilder = new(capacity: 64);

        foreach (byte b in hashBytes)
            hashStringBuilder.Append(value: b.ToString(format: "x2"));
        return hashStringBuilder.ToString();
    }

    // Liberal subtitle filename matcher: 2-3 char language (ISO 639-1 or -3), arbitrary
    // type (alt, full, sign, song, sdh, forced, commentary, director, ...), 3-6 char
    // extension (vtt, ass, srt, ssa, sub, idx, sup, vob, webvtt).  Previously the regex
    // only matched 3-char lang + 3-4 char type + 3-char ext and the consumer methods
    // further filtered to type in (sign, song, full) — both combined silently dropped
    // every "alt" / "sdh" / "forced" subtitle file.
    [GeneratedRegex(pattern: @"(?<lang>[a-zA-Z]{2,3})\.(?<type>\w+)\.(?<ext>\w{3,6})$")]
    private static partial Regex SubtitleFileRegex();

    [GeneratedRegex(pattern: @"#xywh=\d+,\d+,(?<width>\d+),(?<height>\d+)")]
    private static partial Regex ImageDimensions();

    [GeneratedRegex(pattern: @"^video_(?<width>\d+)x(?<height>\d+)(?:_(?:SDR|HDR))?$")]
    private static partial Regex VideoDirectoryRegex();

    // Older encodes name the rendition dir `audio_<lang>` (no codec suffix);
    // newer ones `audio_<lang>_<codec>` (e.g. audio_jpn_aac). Match both — the
    // codec group is optional — or a rescan drops the audio group from the
    // rebuilt master and the title plays silent.
    [GeneratedRegex(pattern: @"^audio_(?<lang>[a-z]{2,3})(?:_(?<codec>\w+))?$")]
    private static partial Regex AudioDirectoryRegex();
}
