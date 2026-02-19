using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// HIGH-14: Verify Kestrel limits are set to finite, generous values
/// instead of null (unlimited).
/// </summary>
[Trait("Category", "Unit")]
public class KestrelLimitsTests
{
    private readonly string _source;

    public KestrelLimitsTests()
    {
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Certificate.cs");
        _source = File.ReadAllText(sourceFile);
    }

    [Fact]
    public void MaxRequestBodySize_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? bodyLine = lines.FirstOrDefault(l =>
            l.Contains("MaxRequestBodySize") && !l.TrimStart().StartsWith("//"));

        Assert.NotNull(bodyLine);
        Assert.DoesNotContain("= null", bodyLine);
        Assert.Contains("100L * 1024 * 1024 * 1024", bodyLine);
    }

    [Fact]
    public void MaxConcurrentConnections_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? connLine = lines.FirstOrDefault(l =>
            l.Contains("MaxConcurrentConnections") &&
            !l.Contains("MaxConcurrentUpgradedConnections") &&
            !l.TrimStart().StartsWith("//"));

        Assert.NotNull(connLine);
        Assert.DoesNotContain("= null", connLine);
        Assert.Contains("1000", connLine);
    }

    [Fact]
    public void MaxConcurrentUpgradedConnections_IsFinite()
    {
        string[] lines = GetKestrelConfigLines();

        string? upgradedLine = lines.FirstOrDefault(l =>
            l.Contains("MaxConcurrentUpgradedConnections") &&
            !l.TrimStart().StartsWith("//"));

        Assert.NotNull(upgradedLine);
        Assert.DoesNotContain("= null", upgradedLine);
        Assert.Contains("500", upgradedLine);
    }

    [Fact]
    public void MaxRequestBufferSize_IsAdaptive()
    {
        // MaxRequestBufferSize = null is intentional — Kestrel manages it adaptively
        string[] lines = GetKestrelConfigLines();

        string? bufferLine = lines.FirstOrDefault(l =>
            l.Contains("MaxRequestBufferSize") &&
            !l.TrimStart().StartsWith("//"));

        Assert.NotNull(bufferLine);
        Assert.Contains("= null", bufferLine);
    }

    [Fact]
    public void ServerHeader_IsDisabled()
    {
        string[] lines = GetKestrelConfigLines();

        string? headerLine = lines.FirstOrDefault(l =>
            l.Contains("AddServerHeader") &&
            !l.TrimStart().StartsWith("//"));

        Assert.NotNull(headerLine);
        Assert.Contains("false", headerLine);
    }

    private string[] GetKestrelConfigLines()
    {
        string[] allLines = _source.Split('\n');

        List<string> configLines = [];
        bool inMethod = false;
        int braceDepth = 0;

        foreach (string line in allLines)
        {
            string trimmed = line.Trim();

            if (trimmed.Contains("void KestrelConfig"))
            {
                inMethod = true;
                continue;
            }

            if (!inMethod) continue;

            if (trimmed.Contains('{')) braceDepth++;
            if (trimmed.Contains('}'))
            {
                braceDepth--;
                if (braceDepth <= 0) break;
            }

            configLines.Add(trimmed);
        }

        return configLines.ToArray();
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
