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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NoMercy.Setup.Server;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace NoMercy.Tests.Providers.Setup;

[Trait(name: "Category", value: "Characterization")]
public class BinariesCloudflaredArchTests
{
    private static readonly string SourcePath = FindSourceFile();

    private static string FindSourceFile()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(paths: [dir, "src", "NoMercy.Setup", "Server", "Binaries.cs"]);
            if (File.Exists(path: candidate))
                return candidate;
            dir = Path.GetDirectoryName(path: dir)!;
        }
        throw new FileNotFoundException(message: "Could not find src/NoMercy.Setup/Server/Binaries.cs");
    }

    private static string GetSourceCode() => File.ReadAllText(path: SourcePath);

    private static string ExtractDownloadCloudflaredMethod(string source)
    {
        int start = source.IndexOf(
            value: "internal async Task DownloadCloudflared()",
            comparisonType: StringComparison.Ordinal
        );
        Assert.True(condition: start >= 0, userMessage: "Could not find DownloadCloudflared method in source");

        int braceStart = source.IndexOf(value: '{', startIndex: start);
        int depth = 0;
        int i = braceStart;
        while (i < source.Length)
        {
            if (source[index: i] == '{')
                depth++;
            else if (source[index: i] == '}')
                depth--;
            if (depth == 0)
                break;
            i++;
        }
        return source[start..(i + 1)];
    }

    [Fact]
    public void DownloadCloudflared_IsAsyncMethod()
    {
        MethodInfo? method = typeof(Binaries).GetMethod(
            name: "DownloadCloudflared",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.NotNull(@object: method);
        Assert.NotNull(@object: method.GetCustomAttribute<AsyncStateMachineAttribute>());
    }

    [Fact]
    public void DownloadCloudflared_MacOS_Arm64_Downloads_Arm64_Binary()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        // Match: OSPlatform.OSX) && ...Architecture.Arm64) ... cloudflared-darwin-XXX.tgz
        // \s* before \) handles formatter putting the closing paren on its own line
        Regex pattern = new(
            pattern: @"OSPlatform\.OSX\).*?Architecture\.Arm64\s*\).*?cloudflared-darwin-(\w+)\.tgz",
            options: RegexOptions.Singleline
        );

        RegexMatch match = pattern.Match(input: method);
        Assert.True(condition: match.Success, userMessage: "Could not find macOS Arm64 branch with darwin asset");
        Assert.Equal(expected: "arm64", actual: match.Groups[groupnum: 1].Value);
    }

    [Fact]
    public void DownloadCloudflared_MacOS_X64_Downloads_Amd64_Binary()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        // Match: OSPlatform.OSX) && ...Architecture.X64) ... cloudflared-darwin-XXX.tgz
        // \s* before \) handles formatter putting the closing paren on its own line
        Regex pattern = new(
            pattern: @"OSPlatform\.OSX\).*?Architecture\.X64\s*\).*?cloudflared-darwin-(\w+)\.tgz",
            options: RegexOptions.Singleline
        );

        RegexMatch match = pattern.Match(input: method);
        Assert.True(condition: match.Success, userMessage: "Could not find macOS X64 branch with darwin asset");
        Assert.Equal(expected: "amd64", actual: match.Groups[groupnum: 1].Value);
    }

    [Fact]
    public void DownloadCloudflared_MacOS_Architectures_Not_Swapped()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        // Extract both macOS branches and verify each downloads the correct architecture
        // \s* before \) handles formatter putting the closing paren on its own line
        Regex arm64Pattern = new(
            pattern: @"OSPlatform\.OSX\).*?Architecture\.Arm64\s*\).*?cloudflared-darwin-(\w+)\.tgz",
            options: RegexOptions.Singleline
        );
        Regex x64Pattern = new(
            pattern: @"OSPlatform\.OSX\).*?Architecture\.X64\s*\).*?cloudflared-darwin-(\w+)\.tgz",
            options: RegexOptions.Singleline
        );

        RegexMatch arm64Match = arm64Pattern.Match(input: method);
        RegexMatch x64Match = x64Pattern.Match(input: method);

        Assert.True(condition: arm64Match.Success, userMessage: "macOS Arm64 branch not found");
        Assert.True(condition: x64Match.Success, userMessage: "macOS X64 branch not found");

        // Arm64 host must download arm64 binary (not amd64)
        Assert.Equal(expected: "arm64", actual: arm64Match.Groups[groupnum: 1].Value);
        // X64 host must download amd64 binary (not arm64)
        Assert.Equal(expected: "amd64", actual: x64Match.Groups[groupnum: 1].Value);
    }

    [Fact]
    public void DownloadCloudflared_Windows_Downloads_Amd64()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());
        Assert.Contains(expectedSubstring: "cloudflared-windows-amd64.exe", actualString: method);
    }

    [Fact]
    public void DownloadCloudflared_Linux_Arm64_Downloads_Arm()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        // \s* before \) handles formatter putting the closing paren on its own line
        Regex pattern = new(
            pattern: @"OSPlatform\.Linux\).*?Architecture\.Arm64\s*\).*?cloudflared-linux-(\w+)""",
            options: RegexOptions.Singleline
        );
        RegexMatch match = pattern.Match(input: method);
        Assert.True(condition: match.Success, userMessage: "Could not find Linux Arm64 branch");
        Assert.Equal(expected: "arm", actual: match.Groups[groupnum: 1].Value);
    }

    [Fact]
    public void DownloadCloudflared_Linux_X64_Downloads_Amd64()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        // \s* before \) handles formatter putting the closing paren on its own line
        Regex pattern = new(
            pattern: @"OSPlatform\.Linux\).*?Architecture\.X64\s*\).*?cloudflared-linux-(\w+)""",
            options: RegexOptions.Singleline
        );
        RegexMatch match = pattern.Match(input: method);
        Assert.True(condition: match.Success, userMessage: "Could not find Linux X64 branch");
        Assert.Equal(expected: "amd64", actual: match.Groups[groupnum: 1].Value);
    }

    [Fact]
    public void DownloadCloudflared_All_Platform_Assets_Present()
    {
        string method = ExtractDownloadCloudflaredMethod(source: GetSourceCode());

        Assert.Contains(expectedSubstring: "cloudflared-windows-amd64.exe", actualString: method);
        Assert.Contains(expectedSubstring: "cloudflared-linux-arm", actualString: method);
        Assert.Contains(expectedSubstring: "cloudflared-linux-amd64", actualString: method);
        Assert.Contains(expectedSubstring: "cloudflared-darwin-arm64.tgz", actualString: method);
        Assert.Contains(expectedSubstring: "cloudflared-darwin-amd64.tgz", actualString: method);
    }
}
