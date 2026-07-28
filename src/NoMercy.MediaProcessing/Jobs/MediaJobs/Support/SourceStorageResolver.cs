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
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

/// <summary>
/// Which storage an encode READS its source through, which is not always the
/// one it writes the output to.
///
/// <para>A remote source names its own driver and always worked. The ordinary
/// local case did not: with no source driver the read fell back to the
/// DESTINATION storage, scoped to the library root, so the path guard refused
/// any source file that was not already inside the library it encodes into.
/// That is the normal way content arrives — the dashboard's add-content picker
/// browses the whole machine — so a file dispatched from an intake folder
/// failed on its own source path with <c>output.path_not_allowed</c> before
/// ffmpeg was ever invoked.</para>
///
/// <para>The coordinator and the child task each resolved this for themselves,
/// with the same fallback written twice. Fixing only the coordinator moved the
/// failure rather than removing it: the queue stopped recording failures, the
/// child tasks kept recording exactly the same one, and the encoder looked
/// healthy while producing nothing. One rule, one place, both callers.</para>
/// </summary>
public static class SourceStorageResolver
{
    /// <summary>
    /// Returns the storage <paramref name="inputFile"/> should be read through.
    /// A source already under the library keeps using
    /// <paramref name="destinationStorage"/>, so the common case allocates
    /// nothing new and the guard still bounds it to the folder it belongs to.
    /// </summary>
    public static IStorage Resolve(
        IStorageFactory storageFactory,
        Ulid? sourceDriverId,
        string inputFile,
        Folder folder,
        IStorage destinationStorage
    )
    {
        if (sourceDriverId.HasValue)
            return storageFactory.For(sourceDriverId.Value, sourceDriverId.Value, string.Empty);

        string? sourceDirectory = DirectoryOf(inputFile);

        if (string.IsNullOrEmpty(sourceDirectory) || IsUnderRoot(folder.Path, inputFile))
            return destinationStorage;

        return storageFactory.For(folder.Id, folder.DriverId, sourceDirectory);
    }

    /// <summary>
    /// The directory part of <paramref name="path"/>, treating BOTH separators
    /// as separators.
    /// <para><see cref="Path.GetDirectoryName(string)"/> cannot: on Linux a
    /// backslash is an ordinary filename character, so a Windows or UNC path
    /// arriving at a Linux server reads as one long file name with no directory
    /// at all. The resolver then falls back to the destination storage — which
    /// is the exact failure this class exists to prevent, reappearing only on
    /// the platform most servers run on.</para>
    /// <para>These paths come from a picker on a Windows box or from a NAS
    /// share, so the separator is part of the data and not a property of the
    /// machine reading it.</para>
    /// </summary>
    internal static string? DirectoryOf(string path)
    {
        int lastSeparator = path.LastIndexOfAny(['/', '\\']);
        return lastSeparator <= 0 ? null : path[..lastSeparator];
    }

    /// <summary>
    /// Whether <paramref name="path"/> sits inside <paramref name="root"/>,
    /// compared the way the filesystem reads it rather than the way the strings
    /// happen to be spelled: separators differ between the folder record and
    /// the picker's output, and Windows does not care about case.
    /// </summary>
    internal static bool IsUnderRoot(string? root, string path)
    {
        if (string.IsNullOrWhiteSpace(root))
            return false;

        string normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
        string normalizedPath = path.Replace('\\', '/');

        return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }
}
