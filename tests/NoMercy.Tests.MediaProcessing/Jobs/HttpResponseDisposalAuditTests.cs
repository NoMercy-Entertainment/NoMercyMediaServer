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
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                if (!HttpResponseDeclarationPattern().IsMatch(trimmed)) continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains("using ")) continue;

                string relative = Path.GetRelativePath(srcDir, file);
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
            if (Directory.Exists(candidate)) return candidate;

            dir = Directory.GetParent(dir)?.FullName;
        }

        string fallback = "/workspaces/NoMercyMediaServer/src";
        if (Directory.Exists(fallback)) return fallback;

        throw new DirectoryNotFoundException("Could not find src/ directory");
    }

    [GeneratedRegex(@"HttpResponseMessage\s+\w+\s*=")]
    private static partial Regex HttpResponseDeclarationPattern();
}
