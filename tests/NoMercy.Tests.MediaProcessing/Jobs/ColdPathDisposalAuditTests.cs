using System.Text.RegularExpressions;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// DISP-04: Audit test verifying that Process.Start, File.OpenWrite/OpenRead/Create
/// results are properly disposed in cold paths.
/// </summary>
[Trait("Category", "Unit")]
public partial class ColdPathDisposalAuditTests
{
    [Fact]
    public void Source_ProcessStart_HasUsingOrDispose()
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

                if (!ProcessStartStaticPattern().IsMatch(trimmed)) continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains("using ")) continue;

                // Allow: lines that call .Dispose() inline
                if (trimmed.Contains(".Dispose()")) continue;

                // Allow: lines that call ?.Dispose() inline
                if (trimmed.Contains("?.Dispose()")) continue;

                // Allow: instance .Start() calls on managed process objects (not static factory)
                if (InstanceStartPattern().IsMatch(trimmed)) continue;

                // Allow: test files
                string relative = Path.GetRelativePath(srcDir, file);
                if (relative.Contains("Test")) continue;

                violations.Add($"{relative}:{i + 1} — {trimmed}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Source_FileOpenWrite_HasUsing()
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

                if (!FileOpenPattern().IsMatch(trimmed)) continue;

                // Allow: lines with 'using' keyword
                if (trimmed.Contains("using ")) continue;

                string relative = Path.GetRelativePath(srcDir, file);
                if (relative.Contains("Test")) continue;

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

    [GeneratedRegex(@"Process\.Start\([""n]")]
    private static partial Regex ProcessStartStaticPattern();

    [GeneratedRegex(@"_\w+\.Start\(\)")]
    private static partial Regex InstanceStartPattern();

    [GeneratedRegex(@"(?<!\w)File\.(OpenWrite|OpenRead|Create)\(")]
    private static partial Regex FileOpenPattern();
}
