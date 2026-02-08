using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NoMercy.Setup;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace NoMercy.Tests.Providers.Setup;

[Trait("Category", "Characterization")]
public class BinariesCloudflaredArchTests
{
    private static readonly string SourcePath = FindSourceFile();

    private static string FindSourceFile()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(dir, "src", "NoMercy.Setup", "Binaries.cs");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        throw new FileNotFoundException("Could not find src/NoMercy.Setup/Binaries.cs");
    }

    private static string GetSourceCode() => File.ReadAllText(SourcePath);

    private static string ExtractDownloadCloudflaredMethod(string source)
    {
        int start = source.IndexOf("private static async Task DownloadCloudflared()", StringComparison.Ordinal);
        Assert.True(start >= 0, "Could not find DownloadCloudflared method in source");

        int braceStart = source.IndexOf('{', start);
        int depth = 0;
        int i = braceStart;
        while (i < source.Length)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}') depth--;
            if (depth == 0) break;
            i++;
        }
        return source[start..(i + 1)];
    }

    [Fact]
    public void DownloadCloudflared_IsAsyncMethod()
    {
        MethodInfo? method = typeof(Binaries).GetMethod(
            "DownloadCloudflared",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AsyncStateMachineAttribute>());
    }

    [Fact]
    public void DownloadCloudflared_MacOS_Arm64_Downloads_Arm64_Binary()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        // Match: OSPlatform.OSX) && ...Architecture.Arm64) ... cloudflared-darwin-XXX.tgz
        Regex pattern = new(
            @"OSPlatform\.OSX\).*?Architecture\.Arm64\).*?cloudflared-darwin-(\w+)\.tgz",
            RegexOptions.Singleline);

        RegexMatch match = pattern.Match(method);
        Assert.True(match.Success, "Could not find macOS Arm64 branch with darwin asset");
        Assert.Equal("arm64", match.Groups[1].Value);
    }

    [Fact]
    public void DownloadCloudflared_MacOS_X64_Downloads_Amd64_Binary()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        // Match: OSPlatform.OSX) && ...Architecture.X64) ... cloudflared-darwin-XXX.tgz
        Regex pattern = new(
            @"OSPlatform\.OSX\).*?Architecture\.X64\).*?cloudflared-darwin-(\w+)\.tgz",
            RegexOptions.Singleline);

        RegexMatch match = pattern.Match(method);
        Assert.True(match.Success, "Could not find macOS X64 branch with darwin asset");
        Assert.Equal("amd64", match.Groups[1].Value);
    }

    [Fact]
    public void DownloadCloudflared_MacOS_Architectures_Not_Swapped()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        // Extract both macOS branches and verify each downloads the correct architecture
        Regex arm64Pattern = new(
            @"OSPlatform\.OSX\).*?Architecture\.Arm64\).*?cloudflared-darwin-(\w+)\.tgz",
            RegexOptions.Singleline);
        Regex x64Pattern = new(
            @"OSPlatform\.OSX\).*?Architecture\.X64\).*?cloudflared-darwin-(\w+)\.tgz",
            RegexOptions.Singleline);

        RegexMatch arm64Match = arm64Pattern.Match(method);
        RegexMatch x64Match = x64Pattern.Match(method);

        Assert.True(arm64Match.Success, "macOS Arm64 branch not found");
        Assert.True(x64Match.Success, "macOS X64 branch not found");

        // Arm64 host must download arm64 binary (not amd64)
        Assert.Equal("arm64", arm64Match.Groups[1].Value);
        // X64 host must download amd64 binary (not arm64)
        Assert.Equal("amd64", x64Match.Groups[1].Value);
    }

    [Fact]
    public void DownloadCloudflared_Windows_Downloads_Amd64()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());
        Assert.Contains("cloudflared-windows-amd64.exe", method);
    }

    [Fact]
    public void DownloadCloudflared_Linux_Arm64_Downloads_Arm()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        Regex pattern = new(
            @"OSPlatform\.Linux\).*?Architecture\.Arm64\).*?cloudflared-linux-(\w+)""",
            RegexOptions.Singleline);
        RegexMatch match = pattern.Match(method);
        Assert.True(match.Success, "Could not find Linux Arm64 branch");
        Assert.Equal("arm", match.Groups[1].Value);
    }

    [Fact]
    public void DownloadCloudflared_Linux_X64_Downloads_Amd64()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        Regex pattern = new(
            @"OSPlatform\.Linux\).*?Architecture\.X64\).*?cloudflared-linux-(\w+)""",
            RegexOptions.Singleline);
        RegexMatch match = pattern.Match(method);
        Assert.True(match.Success, "Could not find Linux X64 branch");
        Assert.Equal("amd64", match.Groups[1].Value);
    }

    [Fact]
    public void DownloadCloudflared_All_Platform_Assets_Present()
    {
        string method = ExtractDownloadCloudflaredMethod(GetSourceCode());

        Assert.Contains("cloudflared-windows-amd64.exe", method);
        Assert.Contains("cloudflared-linux-arm", method);
        Assert.Contains("cloudflared-linux-amd64", method);
        Assert.Contains("cloudflared-darwin-arm64.tgz", method);
        Assert.Contains("cloudflared-darwin-amd64.tgz", method);
    }
}
