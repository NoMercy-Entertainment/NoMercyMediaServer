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
/// DISP-01: Audit tests verifying that Image&lt;Rgba32&gt; objects are properly disposed.
/// Each Image&lt;Rgba32&gt; holds 5-50MB of unmanaged memory. During library scans with
/// thousands of images, undisposed images cause severe memory growth.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public partial class ImageDisposalAuditTests
{
    [Fact]
    public void Source_ImageLoadInLocalScope_HasUsing()
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

                if (!ImageLoadPattern().IsMatch(input: trimmed))
                    continue;

                // Allow: return statements (ownership transferred to caller)
                if (trimmed.Contains(value: "return ") || trimmed.Contains(value: "return\t"))
                    continue;

                // Allow: lines with 'using' keyword on this line or the preceding line
                // (handles multi-line 'using Image<T> img =\n    await Image.LoadAsync(...)')
                if (trimmed.Contains(value: "using "))
                    continue;

                if (i > 0 && lines[i - 1].Trim().Contains(value: "using "))
                    continue;

                string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
            }
        }

        Assert.Empty(collection: violations);
    }

    [Fact]
    public void Source_DownloadCallers_DisposeReturnedImage()
    {
        string srcDir = FindSrcDirectory();
        string[] csFiles = Directory.GetFiles(path: srcDir, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories);

        List<string> violations = [];

        foreach (string file in csFiles)
        {
            // Skip the Download method definitions themselves
            string fileName = Path.GetFileName(path: file);
            if (
                fileName
                is "TmdbImageClient.cs"
                    or "FanArtImageClient.cs"
                    or "CoverArtCoverArtClient.cs"
                    or "NoMercyImageClient.cs"
            )
                continue;

            string content = File.ReadAllText(path: file);
            string[] lines = content.Split(separator: '\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith(value: "//") || trimmed.StartsWith(value: "*"))
                    continue;

                if (!DownloadCallPattern().IsMatch(input: trimmed))
                    continue;

                // Check that the result is disposed: either via 'using' on this or previous line
                bool hasUsing = trimmed.Contains(value: "using ");
                if (i > 0)
                {
                    string prevLine = lines[i - 1].Trim();
                    if (prevLine.Contains(value: "using "))
                        hasUsing = true;
                }

                if (!hasUsing)
                {
                    string relative = Path.GetRelativePath(relativeTo: srcDir, path: file);
                    violations.Add(item: $"{relative}:{i + 1} — {trimmed}");
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

    [GeneratedRegex(pattern: @"Image\.Load(?:Async)?[<\(]")]
    private static partial Regex ImageLoadPattern();

    [GeneratedRegex(
        pattern: @"(?:TmdbImageClient|FanArtImageClient|CoverArtCoverArtClient|NoMercyImageClient)\.Download\s*\("
    )]
    private static partial Regex DownloadCallPattern();
}
