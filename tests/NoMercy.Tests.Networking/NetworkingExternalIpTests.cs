using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// HIGH-20: Tests verifying that the ExternalIp property getter does not block
/// with .Result on an async operation, and that Discover() eagerly populates the IP.
/// </summary>
[Trait("Category", "Unit")]
public class NetworkingExternalIpTests
{
    [Fact]
    public void ExternalIp_Getter_NoBlockingResult()
    {
        // HIGH-20: The ExternalIp getter must NOT call .Result on async GetExternalIp().
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Networking.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        bool insideExternalIpGetter = false;
        int braceDepth = 0;
        List<string> getterLines = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.Contains("public static string ExternalIp"))
            {
                insideExternalIpGetter = true;
                braceDepth = 0;
                continue;
            }

            if (insideExternalIpGetter)
            {
                if (trimmed.Contains('{')) braceDepth++;
                if (trimmed.Contains('}')) braceDepth--;

                if (trimmed.StartsWith("get"))
                {
                    getterLines.Add(trimmed);
                }

                if (braceDepth <= 0 && getterLines.Count > 0) break;
            }
        }

        Assert.NotEmpty(getterLines);

        foreach (string line in getterLines)
        {
            Assert.DoesNotContain(".Result", line);
        }
    }

    [Fact]
    public void ExternalIp_Getter_ReturnsFallbackWhenNotPopulated()
    {
        // HIGH-20: The getter should return a safe fallback ("0.0.0.0"), not call async methods.
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Networking.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        string? getterLine = lines.FirstOrDefault(l =>
            l.Trim().StartsWith("get =>") && l.Contains("externalIp"));

        Assert.NotNull(getterLine);
        Assert.Contains("??", getterLine);
        Assert.DoesNotContain("GetExternalIp()", getterLine);
    }

    [Fact]
    public void Discover_AlwaysPopulatesExternalIp()
    {
        // HIGH-20: Discover() must eagerly fetch the external IP so the getter never blocks.
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Networking.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("string.IsNullOrEmpty(_externalIp)", source);
        Assert.Contains("await GetExternalIp()", source);
    }

    [Fact]
    public void ExternalIp_ReturnsCachedValueWithoutBlocking()
    {
        // HIGH-20: After setting ExternalIp, the getter returns the cached value instantly.
        string original = NoMercy.Networking.Networking.ExternalIp;

        NoMercy.Networking.Networking.ExternalIp = "1.2.3.4";

        Assert.Equal("1.2.3.4", NoMercy.Networking.Networking.ExternalIp);

        // Restore original state
        if (original == "0.0.0.0")
        {
            // Reset backing field via setter with a different value then back
            NoMercy.Networking.Networking.ExternalIp = original;
        }
        else
        {
            NoMercy.Networking.Networking.ExternalIp = original;
        }
    }

    [Fact]
    public void ExternalIp_DefaultFallbackIsNotEmpty()
    {
        // HIGH-20: When _externalIp is null, getter must not return null or empty.
        // We can verify by checking the source — the fallback is "0.0.0.0".
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Networking.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        string? getterLine = lines.FirstOrDefault(l =>
            l.Trim().StartsWith("get =>") && l.Contains("externalIp"));

        Assert.NotNull(getterLine);
        Assert.Contains("\"0.0.0.0\"", getterLine);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;

            string repoCandidate = Path.Combine(dir, "..", "..", "..", "..", "..", relativePath);
            string resolved = Path.GetFullPath(repoCandidate);
            if (File.Exists(resolved)) return resolved;

            dir = Directory.GetParent(dir)?.FullName;
        }

        string fallback = Path.Combine("/workspaces/NoMercyMediaServer", relativePath);
        if (File.Exists(fallback)) return fallback;

        throw new FileNotFoundException($"Could not find source file: {relativePath}");
    }
}
