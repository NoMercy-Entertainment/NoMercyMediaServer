using System.Text.RegularExpressions;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// DISP-03: Audit test verifying that TagLib.File and TagFile.Create() results are properly disposed.
/// TagLib.File implements IDisposable and holds file handles. Leaking these inside
/// Parallel.ForEach loops means scanning 1000 songs leaks 1000 file handles.
/// </summary>
[Trait("Category", "Unit")]
public partial class TagFileDisposalAuditTests
{
    [Fact]
    public void Source_TagLibFileCreate_HasUsing()
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

                if (!TagLibFileCreatePattern().IsMatch(trimmed)) continue;

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

    // Matches: TagLib.File xxx = TagLib.File.Create(...)  or  FileTag? xxx = FileTag.Create(...)
    [GeneratedRegex(@"(TagLib\.File|FileTag\??)\s+\w+\s*=\s*(TagLib\.File|FileTag)\.Create")]
    private static partial Regex TagLibFileCreatePattern();
}
