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
            StorageEndpoint endpoint = DetectEndpoint(absolutePath: item.AbsoluteRootPath);
            if (!byEndpoint.TryGetValue(key: endpoint.Key, value: out List<FolderRootInput>? group))
            {
                group = [];
                byEndpoint[key: endpoint.Key] = group;
            }

            group.Add(item: item);
        }

        List<DriverGroup> result = [];
        foreach (KeyValuePair<string, List<FolderRootInput>> kv in byEndpoint)
        {
            string endpointKey = kv.Key;
            List<FolderRootInput> members = kv.Value;
            StorageEndpointKind kind = DetectEndpoint(absolutePath: members[index: 0].AbsoluteRootPath).Kind;
            // The endpoint kind drives path math (UNC uses '\\' separators and a
            // \\server\share root), but a UNC folder is still served by the local
            // driver — Windows mounts the share and LocalStorage reads it natively.
            // The dedicated SMB driver is only used for folders configured as an
            // explicit SMB endpoint, never inferred from a UNC path here.
            const string driverType = "local";

            string driverRoot = ComputeCommonAncestor(
                absolutePaths: members.Select(selector: m => m.AbsoluteRootPath).ToList(),
                kind: kind
            );

            List<FolderAssignment> assignments = members
                .Select(selector: member =>
                {
                    string subPath = ComputeSubPath(driverRoot: driverRoot, absolutePath: member.AbsoluteRootPath, kind: kind);
                    return new FolderAssignment(FolderId: member.FolderId, SubPath: subPath);
                })
                .ToList();

            result.Add(item: new(DriverRoot: driverRoot, DriverType: driverType, Folders: assignments));
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
        string normalized = absolutePath.Replace(oldChar: '/', newChar: '\\');

        if (normalized.StartsWith(value: @"\\", comparisonType: StringComparison.Ordinal))
        {
            string withoutLeading = normalized[2..];
            int serverEnd = withoutLeading.IndexOf(value: '\\');
            if (serverEnd < 0)
                return new(Key: @"\\" + withoutLeading, Kind: StorageEndpointKind.Smb);

            string server = withoutLeading[..serverEnd];
            string rest = withoutLeading[(serverEnd + 1)..];
            int shareEnd = rest.IndexOf(value: '\\');
            string share = shareEnd < 0 ? rest : rest[..shareEnd];

            string endpointKey = $@"\\{server}\{share}";
            return new(Key: endpointKey, Kind: StorageEndpointKind.Smb);
        }

        if (normalized is [_, ':', ..])
        {
            string driveKey = normalized[..2].ToUpperInvariant();
            return new(Key: driveKey, Kind: StorageEndpointKind.Local);
        }

        return new(Key: "/", Kind: StorageEndpointKind.Local);
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
            throw new ArgumentException(message: "Path list must not be empty.", paramName: nameof(absolutePaths));

        if (absolutePaths.Count == 1)
            return absolutePaths[index: 0];

        char separator = kind == StorageEndpointKind.Smb ? '\\' : LocalSeparator(path: absolutePaths[index: 0]);

        List<string[]> splitPaths = absolutePaths
            .Select(selector: path => SplitPath(path: path, separator: separator))
            .ToList();

        int minLength = splitPaths.Min(selector: segments => segments.Length);
        int commonSegments = 0;

        for (int segmentIndex = 0; segmentIndex < minLength; segmentIndex++)
        {
            string reference = splitPaths[index: 0][segmentIndex];
            bool allMatch = splitPaths.All(predicate: segments =>
                string.Equals(a: segments[segmentIndex], b: reference, comparisonType: StringComparison.OrdinalIgnoreCase)
            );

            if (!allMatch)
                break;

            commonSegments = segmentIndex + 1;
        }

        if (commonSegments == 0)
            return kind == StorageEndpointKind.Smb ? @"\\" : "/";

        return JoinSegments(segments: splitPaths[index: 0][..commonSegments], separator: separator, kind: kind);
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
        char separator = kind == StorageEndpointKind.Smb ? '\\' : LocalSeparator(path: absolutePath);
        string normalizedRoot = driverRoot.TrimEnd(trimChars: ['/', '\\']);
        string normalizedPath = absolutePath.TrimEnd(trimChars: ['/', '\\']);

        if (string.Equals(a: normalizedRoot, b: normalizedPath, comparisonType: StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (
            normalizedPath.StartsWith(
                value: normalizedRoot + separator,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
            return normalizedPath[(normalizedRoot.Length + 1)..];

        return normalizedPath;
    }

    private static char LocalSeparator(string path)
    {
        return path is [_, ':', ..] ? '\\' : '/';
    }

    private static string[] SplitPath(string path, char separator)
    {
        return path.Replace(oldChar: separator == '\\' ? '/' : '\\', newChar: separator)
            .Split(separator: separator, options: StringSplitOptions.RemoveEmptyEntries);
    }

    private static string JoinSegments(string[] segments, char separator, StorageEndpointKind kind)
    {
        if (kind == StorageEndpointKind.Smb)
            return @"\\" + string.Join(separator: '\\', value: segments);

        if (segments.Length == 0)
            return "/";

        string first = segments[0];
        bool isWindowsDrive = first is [_, ':'];

        if (isWindowsDrive)
        {
            if (segments.Length == 1)
                return first + separator;
            return first + separator + string.Join(separator: separator, value: segments[1..]);
        }

        return "/" + string.Join(separator: '/', value: segments);
    }
}
