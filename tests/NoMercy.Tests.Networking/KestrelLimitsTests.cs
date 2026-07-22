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

using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// HIGH-14: Verify Kestrel limits are set to finite, generous values
/// instead of null (unlimited).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class KestrelLimitsTests
{
    private readonly string _source;

    public KestrelLimitsTests()
    {
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Certificate/CertificateService.cs"
        );
        _source = File.ReadAllText(path: sourceFile);
    }

    [Fact]
    public void MaxRequestBodySize_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? bodyLine = lines.FirstOrDefault(predicate: l =>
            l.Contains(value: "MaxRequestBodySize") && !l.TrimStart().StartsWith(value: "//")
        );

        Assert.NotNull(@object: bodyLine);
        Assert.DoesNotContain(expectedSubstring: "= null", actualString: bodyLine);
        Assert.Contains(expectedSubstring: "100L * 1024 * 1024 * 1024", actualString: bodyLine);
    }

    [Fact]
    public void MaxConcurrentConnections_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? connLine = lines.FirstOrDefault(predicate: l =>
            l.Contains(value: "MaxConcurrentConnections")
            && !l.Contains(value: "MaxConcurrentUpgradedConnections")
            && !l.TrimStart().StartsWith(value: "//")
        );

        Assert.NotNull(@object: connLine);
        Assert.DoesNotContain(expectedSubstring: "= null", actualString: connLine);
        Assert.Contains(expectedSubstring: "1000", actualString: connLine);
    }

    [Fact]
    public void MaxConcurrentUpgradedConnections_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? upgradedLine = lines.FirstOrDefault(predicate: l =>
            l.Contains(value: "MaxConcurrentUpgradedConnections") && !l.TrimStart().StartsWith(value: "//")
        );

        Assert.NotNull(@object: upgradedLine);
        Assert.DoesNotContain(expectedSubstring: "= null", actualString: upgradedLine);
        Assert.Contains(expectedSubstring: "500", actualString: upgradedLine);
    }

    [Fact]
    public void MaxRequestBufferSize_IsAdaptive()
    {
        // MaxRequestBufferSize = null is intentional — Kestrel manages it adaptively
        string[] lines = GetKestrelConfigLines();

        string? bufferLine = lines.FirstOrDefault(predicate: l =>
            l.Contains(value: "MaxRequestBufferSize") && !l.TrimStart().StartsWith(value: "//")
        );

        Assert.NotNull(@object: bufferLine);
        Assert.Contains(expectedSubstring: "= null", actualString: bufferLine);
    }

    [Fact]
    public void ServerHeader_IsDisabled()
    {
        string[] lines = GetKestrelConfigLines();

        string? headerLine = lines.FirstOrDefault(predicate: l =>
            l.Contains(value: "AddServerHeader") && !l.TrimStart().StartsWith(value: "//")
        );

        Assert.NotNull(@object: headerLine);
        Assert.Contains(expectedSubstring: "false", actualString: headerLine);
    }

    private string[] GetKestrelConfigLines()
    {
        string[] allLines = _source.Split(separator: '\n');

        List<string> configLines = [];
        bool inMethod = false;
        int braceDepth = 0;

        foreach (string line in allLines)
        {
            string trimmed = line.Trim();

            if (trimmed.Contains(value: "void KestrelConfig"))
            {
                inMethod = true;
                continue;
            }

            if (!inMethod)
                continue;

            if (trimmed.Contains(value: '{'))
                braceDepth++;
            if (trimmed.Contains(value: '}'))
            {
                braceDepth--;
                if (braceDepth <= 0)
                    break;
            }

            configLines.Add(item: trimmed);
        }

        return configLines.ToArray();
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            string repoCandidate = Path.Combine(paths: [dir, "..", "..", "..", "..", "..", relativePath]);
            string resolved = Path.GetFullPath(path: repoCandidate);
            if (File.Exists(path: resolved))
                return resolved;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        string fallback = Path.Combine(path1: "/workspaces/NoMercyMediaServer", path2: relativePath);
        if (File.Exists(path: fallback))
            return fallback;

        throw new FileNotFoundException(message: $"Could not find source file: {relativePath}");
    }
}
