using System.Diagnostics;

namespace NoMercy.Tests.Encoder.Integration;

/// <summary>
/// Resolves the path to the nomercy-ffmpeg fork for integration tests.
///
/// Real-encode integration tests exercise custom muxers (e.g. <c>spritevtt</c>
/// with its <c>-vtt_filename</c> private option) that exist only in the
/// nomercy-ffmpeg fork. Pointing at the system PATH "ffmpeg" — which on dev
/// machines is usually a stock build like gyan.dev — produces
/// <c>Unrecognized option 'vtt_filename'</c> from ffmpeg and the test fails
/// for an environment reason, not a code reason.
///
/// Resolution order:
///   1. <c>NOMERCY_FFMPEG_PATH</c> env var (CI overrides this).
///   2. Standard dev install: <c>%LOCALAPPDATA%/NoMercy_dev/binaries/ffmpeg</c>
///      or <c>~/.local/share/NoMercy_dev/binaries/ffmpeg</c>.
///   3. Standard prod install: <c>%LOCALAPPDATA%/NoMercy/binaries/ffmpeg</c>
///      or <c>~/.local/share/NoMercy/binaries/ffmpeg</c>.
/// Returns null when no fork binary is found — caller skips the test rather
/// than running it against stock ffmpeg and reporting a false failure.
/// </summary>
internal static class NoMercyFfmpegProbe
{
    private const string SpritevttMarker = "spritevtt";

    public static string? ResolveFfmpegPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("NOMERCY_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        string binaryName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        foreach (string root in CandidateRoots())
        {
            string candidate = Path.Combine(root, "binaries", "ffmpeg", binaryName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string? ResolveFfprobePath(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return null;

        string dir = Path.GetDirectoryName(ffmpegPath) ?? string.Empty;
        string probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        string candidate = Path.Combine(dir, probeName);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Confirms the resolved binary is actually the nomercy-ffmpeg fork by
    /// asking it about the spritevtt muxer. Stock ffmpeg reports
    /// "Unknown muxer" on stderr; the fork prints the muxer's options block
    /// to stdout. Distinguishes a renamed binary from a true fork.
    /// </summary>
    public static bool SupportsSpritevtt(string ffmpegPath)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-h");
            psi.ArgumentList.Add("muxer=spritevtt");

            using Process process = Process.Start(psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            string combined = stdout + stderr;
            return combined.Contains(SpritevttMarker, StringComparison.OrdinalIgnoreCase)
                && !combined.Contains("Unknown muxer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> CandidateRoots()
    {
        string? localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string? home = Environment.GetEnvironmentVariable("HOME");

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "NoMercy_dev");
            yield return Path.Combine(localAppData, "NoMercy");
        }

        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".local", "share", "NoMercy_dev");
            yield return Path.Combine(home, ".local", "share", "NoMercy");
        }
    }
}
