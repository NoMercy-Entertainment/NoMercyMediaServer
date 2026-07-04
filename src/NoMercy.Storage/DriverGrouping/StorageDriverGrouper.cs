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

namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// Pure, DB-free function that groups a list of (folderId, absoluteRootPath)
/// inputs into one <see cref="DriverGroup"/> per shared storage endpoint.
///
/// Rules:
///   - Folders on different endpoints are NEVER merged.
///   - Endpoint detection groups by UNC share (\\server\share) or drive/mount; the
///     persisted driver type is always "local" (LocalStorage serves UNC natively —
///     SMB-with-credentials is a future driver, not wired in StorageFactory yet).
///   - Driver root = longest common ancestor DIRECTORY shared by the folders in a group.
///   - Each folder's SubPath = its path relative to that driver root.
///   - A single-folder group is rooted at the folder itself (SubPath = "").
///
/// This function has zero I/O and is fully unit-testable.
/// </summary>
public static class StorageDriverGrouper
{
    /// <summary>
    /// Groups <paramref name="inputs"/> by shared storage endpoint and computes
    /// one <see cref="DriverGroup"/> per endpoint.
    /// </summary>
    public static IReadOnlyList<DriverGroup> Group(IEnumerable<FolderRootInput> inputs)
    {
        List<FolderRootInput> inputList = inputs.ToList();
        if (inputList.Count == 0)
            return [];

        Dictionary<string, List<FolderRootInput>> byEndpoint = [];

        foreach (FolderRootInput item in inputList)
        {
            StorageEndpoint endpoint = DetectEndpoint(item.AbsoluteRootPath);
            if (!byEndpoint.TryGetValue(endpoint.Key, out List<FolderRootInput>? group))
            {
                group = [];
                byEndpoint[endpoint.Key] = group;
            }

            group.Add(item);
        }

        List<DriverGroup> result = [];
        foreach (KeyValuePair<string, List<FolderRootInput>> kv in byEndpoint)
        {
            string endpointKey = kv.Key;
            List<FolderRootInput> members = kv.Value;
            StorageEndpointKind kind = DetectEndpoint(members[0].AbsoluteRootPath).Kind;
            // The endpoint kind drives path math (UNC uses '\\' separators and a
            // \\server\share root), but a UNC folder is still served by the local
            // driver — Windows mounts the share and LocalStorage reads it natively.
            // The dedicated SMB driver is only used for folders configured as an
            // explicit SMB endpoint, never inferred from a UNC path here.
            const string driverType = "local";

            string driverRoot = ComputeCommonAncestor(
                members.Select(m => m.AbsoluteRootPath).ToList(),
                kind
            );

            List<FolderAssignment> assignments = members
                .Select(member =>
                {
                    string subPath = ComputeSubPath(driverRoot, member.AbsoluteRootPath, kind);
                    return new FolderAssignment(member.FolderId, subPath);
                })
                .ToList();

            result.Add(new DriverGroup(driverRoot, driverType, assignments));
        }

        return result;
    }

    /// <summary>
    /// Detects the storage endpoint of an absolute path.
    /// UNC paths (\\server\share\...) → endpoint key = \\server\share, kind = Smb.
    /// Everything else → endpoint key = drive letter (Windows) or "/" (POSIX), kind = Local.
    /// </summary>
    internal static StorageEndpoint DetectEndpoint(string absolutePath)
    {
        string normalized = absolutePath.Replace('/', '\\');

        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            string withoutLeading = normalized[2..];
            int serverEnd = withoutLeading.IndexOf('\\');
            if (serverEnd < 0)
                return new StorageEndpoint(@"\\" + withoutLeading, StorageEndpointKind.Smb);

            string server = withoutLeading[..serverEnd];
            string rest = withoutLeading[(serverEnd + 1)..];
            int shareEnd = rest.IndexOf('\\');
            string share = shareEnd < 0 ? rest : rest[..shareEnd];

            string endpointKey = $@"\\{server}\{share}";
            return new StorageEndpoint(endpointKey, StorageEndpointKind.Smb);
        }

        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            string driveKey = normalized[..2].ToUpperInvariant();
            return new StorageEndpoint(driveKey, StorageEndpointKind.Local);
        }

        return new StorageEndpoint("/", StorageEndpointKind.Local);
    }

    /// <summary>
    /// Computes the longest common ancestor directory of a list of absolute paths.
    /// For a single path, returns that path itself (the folder IS the root).
    /// For multiple paths, walks up until all paths share the prefix.
    /// Never returns a file path — result is always a directory.
    /// </summary>
    internal static string ComputeCommonAncestor(
        IReadOnlyList<string> absolutePaths,
        StorageEndpointKind kind
    )
    {
        if (absolutePaths.Count == 0)
            throw new ArgumentException("Path list must not be empty.", nameof(absolutePaths));

        if (absolutePaths.Count == 1)
            return absolutePaths[0];

        char separator = kind == StorageEndpointKind.Smb ? '\\' : LocalSeparator(absolutePaths[0]);

        List<string[]> splitPaths = absolutePaths
            .Select(path => SplitPath(path, separator))
            .ToList();

        int minLength = splitPaths.Min(segments => segments.Length);
        int commonSegments = 0;

        for (int segmentIndex = 0; segmentIndex < minLength; segmentIndex++)
        {
            string reference = splitPaths[0][segmentIndex];
            bool allMatch = splitPaths.All(segments =>
                string.Equals(segments[segmentIndex], reference, StringComparison.OrdinalIgnoreCase)
            );

            if (!allMatch)
                break;

            commonSegments = segmentIndex + 1;
        }

        if (commonSegments == 0)
            return kind == StorageEndpointKind.Smb ? @"\\" : "/";

        return JoinSegments(splitPaths[0][..commonSegments], separator, kind);
    }

    /// <summary>
    /// Computes the relative sub-path from <paramref name="driverRoot"/> to
    /// <paramref name="absolutePath"/>. Returns empty string when they are equal.
    /// </summary>
    internal static string ComputeSubPath(
        string driverRoot,
        string absolutePath,
        StorageEndpointKind kind
    )
    {
        char separator = kind == StorageEndpointKind.Smb ? '\\' : LocalSeparator(absolutePath);
        string normalizedRoot = driverRoot.TrimEnd('/', '\\');
        string normalizedPath = absolutePath.TrimEnd('/', '\\');

        if (string.Equals(normalizedRoot, normalizedPath, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (
            normalizedPath.StartsWith(
                normalizedRoot + separator,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return normalizedPath[(normalizedRoot.Length + 1)..];

        return normalizedPath;
    }

    private static char LocalSeparator(string path)
    {
        return path.Length >= 2 && path[1] == ':' ? '\\' : '/';
    }

    private static string[] SplitPath(string path, char separator)
    {
        return path.Replace(separator == '\\' ? '/' : '\\', separator)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string JoinSegments(string[] segments, char separator, StorageEndpointKind kind)
    {
        if (kind == StorageEndpointKind.Smb)
            return @"\\" + string.Join('\\', segments);

        if (segments.Length == 0)
            return "/";

        string first = segments[0];
        bool isWindowsDrive = first.Length == 2 && first[1] == ':';

        if (isWindowsDrive)
        {
            if (segments.Length == 1)
                return first + separator;
            return first + separator + string.Join(separator, segments[1..]);
        }

        return "/" + string.Join('/', segments);
    }
}
