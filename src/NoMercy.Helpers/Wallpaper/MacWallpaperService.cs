using System.Diagnostics;
using System.Runtime.Versioning;

namespace NoMercy.Helpers.Wallpaper;

[SupportedOSPlatform("macos")]
public class MacWallpaperService : IWallpaperService
{
    public bool IsSupported => true;

    public void Set(string imagePath, WallpaperStyle style, string hexColor)
    {
        ApplyWallpaper(imagePath);
    }

    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor)
    {
        Set(imagePath, style, hexColor);
    }

    public void Restore()
    {
        // macOS doesn't expose a simple API to restore the previous wallpaper.
        // The OS manages wallpaper history internally.
    }

    private static void ApplyWallpaper(string imagePath)
    {
        // Try the System Events approach first (works on macOS 14+ Sonoma)
        string script =
            $"tell application \"System Events\" to tell every desktop to set picture to \"{imagePath}\"";

        if (!RunOsascript(script))
        {
            // Fallback for older macOS versions
            string fallbackScript =
                $"tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"";
            RunOsascript(fallbackScript);
        }
    }

    private static bool RunOsascript(string script)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e '{script}'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
