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
/// HIGH-20b: Audit tests verifying that all GC.Collect() band-aid calls have been
/// removed from the codebase. GC.Collect() causes stop-the-world pauses that freeze
/// all threads, causing playback stuttering during library scans.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public partial class GcCollectAuditTests
{
    [Fact]
    public void Source_NoGcCollectCalls()
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

                if (GcCollectPattern().IsMatch(input: trimmed))
                {
                    string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                    violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
                }
            }
        }

        Assert.Empty(collection: violations);
    }

    [Fact]
    public void Source_NoGcWaitForFullGCComplete()
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

                if (GcWaitForFullGcPattern().IsMatch(input: trimmed))
                {
                    string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                    violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
                }
            }
        }

        Assert.Empty(collection: violations);
    }

    [Fact]
    public void Source_NoGcWaitForPendingFinalizers()
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

                if (GcWaitForPendingFinalizersPattern().IsMatch(input: trimmed))
                {
                    string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                    violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
                }
            }
        }

        Assert.Empty(collection: violations);
    }

    [Fact]
    public void Source_NoFinalizersCallingDispose()
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

                if (FinalizerPattern().IsMatch(input: trimmed))
                {
                    // Check if the next non-empty lines contain Dispose()
                    for (int j = i + 1; j < Math.Min(val1: i + 5, val2: lines.Length); j++)
                    {
                        string next = lines[j].Trim();
                        if (next.Contains(value: "Dispose()"))
                        {
                            string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                            violations.Add(item: $"{relative}:{i + 1} — finalizer calls Dispose()");
                            break;
                        }
                    }
                }
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

    [GeneratedRegex(pattern: @"GC\.Collect\s*\(")]
    private static partial Regex GcCollectPattern();

    [GeneratedRegex(pattern: @"GC\.WaitForFullGCComplete\s*\(")]
    private static partial Regex GcWaitForFullGcPattern();

    [GeneratedRegex(pattern: @"GC\.WaitForPendingFinalizers\s*\(")]
    private static partial Regex GcWaitForPendingFinalizersPattern();

    [GeneratedRegex(pattern: @"~\w+\s*\(\)")]
    private static partial Regex FinalizerPattern();
}
