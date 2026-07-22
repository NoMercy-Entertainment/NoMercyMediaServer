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

using System.Text.RegularExpressions;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// DISP-03: Audit test verifying that TagLib.File and TagFile.Create() results are properly disposed.
/// TagLib.File implements IDisposable and holds file handles. Leaking these inside
/// Parallel.ForEach loops means scanning 1000 songs leaks 1000 file handles.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public partial class TagFileDisposalAuditTests
{
    [Fact]
    public void Source_TagLibFileCreate_HasUsing()
    {
        string srcDir = FindSrcDirectory();
        string[] csFiles = Directory.GetFiles(path: srcDir, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories);

        List<string> violations = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(path: file);
            string[] lines = content.Split(separator: '\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                    continue;

                if (!TagLibFileCreatePattern().IsMatch(input: trimmed))
                    continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains(value: "using "))
                    continue;

                string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
            }
        }

        Assert.Empty(collection: violations);
    }

    private static string FindSrcDirectory()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: "src");
            if (Directory.Exists(path: candidate))
                return candidate;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        string fallback = "/workspaces/NoMercyMediaServer/src";
        if (Directory.Exists(path: fallback))
            return fallback;

        throw new DirectoryNotFoundException(message: "Could not find src/ directory");
    }

    // Matches: TagLib.File xxx = TagLib.File.Create(...)  or  FileTag? xxx = FileTag.Create(...)
    [GeneratedRegex(pattern: @"(TagLib\.File|FileTag\??)\s+\w+\s*=\s*(TagLib\.File|FileTag)\.Create")]
    private static partial Regex TagLibFileCreatePattern();
}
