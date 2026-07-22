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
/// DISP-04: Audit test verifying that Process.Start, File.OpenWrite/OpenRead/Create
/// results are properly disposed in cold paths.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public partial class ColdPathDisposalAuditTests
{
    [Fact]
    public void Source_ProcessStart_HasUsingOrDispose()
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

                if (!ProcessStartStaticPattern().IsMatch(input: trimmed))
                    continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains(value: "using "))
                    continue;

                // Allow: lines that call .Dispose() inline
                if (trimmed.Contains(value: ".Dispose()"))
                    continue;

                // Allow: lines that call ?.Dispose() inline
                if (trimmed.Contains(value: "?.Dispose()"))
                    continue;

                // Allow: instance .Start() calls on managed process objects (not static factory)
                if (InstanceStartPattern().IsMatch(input: trimmed))
                    continue;

                // Allow: test files
                string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                if (relative.Contains(value: "Test"))
                    continue;

                violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
            }
        }

        Assert.Empty(collection: violations);
    }

    [Fact]
    public void Source_FileOpenWrite_HasUsing()
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

                if (!FileOpenPattern().IsMatch(input: trimmed))
                    continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains(value: "using "))
                    continue;

                string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                if (relative.Contains(value: "Test"))
                    continue;

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

    [GeneratedRegex(pattern: @"Process\.Start\([""n]")]
    private static partial Regex ProcessStartStaticPattern();

    [GeneratedRegex(pattern: @"_\w+\.Start\(\)")]
    private static partial Regex InstanceStartPattern();

    [GeneratedRegex(pattern: @"(?<!\w)File\.(OpenWrite|OpenRead|Create)\(")]
    private static partial Regex FileOpenPattern();
}
