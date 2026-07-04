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
/// DISP-02: Audit test verifying that HttpResponseMessage objects are properly disposed.
/// HttpResponseMessage implements IDisposable and holds network buffers.
/// Every API call that doesn't dispose the response leaks memory.
/// </summary>
[Trait("Category", "Unit")]
public partial class HttpResponseDisposalAuditTests
{
    [Fact]
    public void Source_HttpResponseMessage_HasUsing()
    {
        string srcDir = FindSrcDirectory();
        string[] csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);

        List<string> violations = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string[] lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*"))
                    continue;

                if (!HttpResponseDeclarationPattern().IsMatch(trimmed))
                    continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains("using "))
                    continue;

                // Allow: the declaration is the first argument of a multi-line
                // `using (\n    HttpResponseMessage x = ...\n)` statement — the
                // formatter puts the `using (` opener on its own line above,
                // so this line alone doesn't contain the keyword.
                bool multiLineUsingOpener = false;
                for (int look = i - 1; look >= Math.Max(0, i - 3); look--)
                {
                    if (lines[look].Trim() == "using (")
                    {
                        multiLineUsingOpener = true;
                        break;
                    }
                }
                if (multiLineUsingOpener)
                    continue;

                string relative = Path.GetRelativePath(srcDir, file);

                // Allow: ownership explicitly transferred to HttpResponseStream within a few lines
                // (the wrapper disposes both the response and its content stream on close)
                bool transferredToWrapper = false;
                for (int look = i + 1; look < Math.Min(i + 6, lines.Length); look++)
                {
                    if (lines[look].Contains("new HttpResponseStream(", StringComparison.Ordinal))
                    {
                        transferredToWrapper = true;
                        break;
                    }
                }
                if (transferredToWrapper)
                    continue;

                violations.Add($"{relative}:{i + 1} — {trimmed}");
            }
        }

        Assert.Empty(violations);
    }

    private static string FindSrcDirectory()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "src");
            if (Directory.Exists(candidate))
                return candidate;

            dir = Directory.GetParent(dir)?.FullName;
        }

        string fallback = "/workspaces/NoMercyMediaServer/src";
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException("Could not find src/ directory");
    }

    [GeneratedRegex(@"HttpResponseMessage\s+\w+\s*=")]
    private static partial Regex HttpResponseDeclarationPattern();
}
