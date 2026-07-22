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

using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Filesystem operations for the dashboard folder picker. Uses
/// <see cref="IStorageDriver"/> directly (not <see cref="IStorage"/>)
/// because the picker legitimately browses paths the user hasn't added
/// to any library yet, so the path-guard allowlist on
/// <see cref="IStorage"/> doesn't apply here.
/// </summary>
public class FilesystemRepository(IStorageDriver driver)
{
    public (string? parent, List<DirectoryTree> entries) List(string folder, bool withEmpty)
    {
        if (string.IsNullOrEmpty(value: folder))
            return (null, []);

        if (!driver.DirectoryExists(path: folder))
            return (null, []);

        List<DirectoryTree> entries;
        try
        {
            entries = EnumerateChildDirectories(folder: folder)
                .Select(selector: child => Build(parent: folder, path: child, withEmpty: withEmpty))
                .OrderBy(keySelector: e => e.Path, comparer: StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException)
        {
            return (ParentOf(folder: folder), []);
        }
        catch (UnauthorizedAccessException)
        {
            return (ParentOf(folder: folder), []);
        }

        return (ParentOf(folder: folder), entries);
    }

    public (string path, string? parent, List<DirectoryTree> entries) Home(bool withEmpty)
    {
        string home = Environment.GetFolderPath(
            folder: Environment.SpecialFolder.UserProfile,
            option: Environment.SpecialFolderOption.DoNotVerify
        );

        if (string.IsNullOrEmpty(value: home) || !driver.DirectoryExists(path: home))
            home = "/";

        (string? parent, List<DirectoryTree> entries) = List(folder: home, withEmpty: withEmpty);
        return (home, parent, entries);
    }

    public List<DirectoryTree> Roots(bool withEmpty)
    {
        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
        {
            // Skip drive types whose metadata reads can block on OS-level
            // retry timeouts (a dead Z:\ network share or empty CD-ROM
            // adds ~30s each). Users type those paths into /ls directly.
            DriveInfo[] drives = DriveInfo.GetDrives();
            return drives
                .Where(predicate: d =>
                    d.DriveType != DriveType.Network
                    && d.DriveType != DriveType.Unknown
                    && d.DriveType != DriveType.NoRootDirectory
                )
                .AsParallel()
                .Select(selector: BuildRoot)
                .Where(predicate: entry => entry is not null)
                .Select(selector: entry => entry!)
                .OrderBy(keySelector: e => e.FullPath, comparer: StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        (_, List<DirectoryTree> entries) = List(folder: "/", withEmpty: withEmpty);
        return entries;
    }

    private DirectoryTree? BuildRoot(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady)
                return null;

            DirectoryTree entry = Build(parent: drive.RootDirectory.ToString(), path: "", withEmpty: false);

            try
            {
                if (!string.IsNullOrEmpty(value: drive.VolumeLabel))
                    entry.Subtitle = drive.VolumeLabel;
            }
            catch
            {
                // VolumeLabel can throw on slow/transitional drives; we
                // already have a valid entry, just skip the label.
            }

            return entry;
        }
        catch
        {
            return null;
        }
    }

    public string Mkdir(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(value: parent))
            throw new ArgumentException(message: "parent is required", paramName: nameof(parent));
        if (string.IsNullOrWhiteSpace(value: name))
            throw new ArgumentException(message: "name is required", paramName: nameof(name));

        if (name.IndexOfAny(anyOf: Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException(message: "name contains invalid characters", paramName: nameof(name));

        if (!driver.DirectoryExists(path: parent))
            throw new DirectoryNotFoundException(message: $"parent does not exist: {parent}");

        string fullPath = Path.Combine(path1: parent, path2: name);
        if (driver.DirectoryExists(path: fullPath))
            return fullPath;

        driver.CreateDirectory(path: fullPath);
        return fullPath;
    }

    private DirectoryTree Build(string parent, string path, bool withEmpty)
    {
        // withEmpty intentionally ignored — populating IsEmpty per entry
        // means an extra TopDirectoryOnly enumeration for every child
        // folder, which kills picker latency on big trees. Clients can
        // still send the flag for wire compat but the field stays null.
        _ = withEmpty;
        return new(parent: parent, path: path);
    }

    private IEnumerable<string> EnumerateChildDirectories(string folder)
    {
        foreach (
            string entry in driver.EnumerateFileSystemEntries(
                directory: folder,
                searchPattern: "*",
                option: SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!driver.DirectoryExists(path: entry))
                continue;
            if (IsBrowsable(path: entry))
                yield return entry;
        }
    }

    private bool IsBrowsable(string path)
    {
        string name = Path.GetFileName(path: path);
        if (string.IsNullOrEmpty(value: name))
            return true;

        // Cross-platform name conventions: Unix dotfiles + Windows
        // sentinel folders (`$RECYCLE.BIN`, `$EXTEND`, ...).
        if (name.StartsWith(value: '.') || name.StartsWith(value: '$'))
            return false;

        // Windows Hidden / System attributes for everything else
        // (`System Volume Information`, `Recovery`, `Config.Msi`, ...).
        return !driver.IsHidden(path: path);
    }

    private static string? ParentOf(string folder)
    {
        if (string.IsNullOrEmpty(value: folder) || folder == "/")
            return null;

        try
        {
            return Path.GetDirectoryName(
                path: folder.TrimEnd(trimChars: [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
            );
        }
        catch
        {
            return null;
        }
    }
}
