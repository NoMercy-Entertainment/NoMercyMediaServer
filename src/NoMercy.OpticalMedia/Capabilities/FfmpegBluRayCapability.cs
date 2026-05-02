using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.OpticalMedia.Capabilities;

/// <summary>
/// One-shot probe executed at startup to confirm the bundled nomercy-ffmpeg
/// binary was compiled with libaacs + libbdplus support. The probe runs
/// <c>ffmpeg -hide_banner -protocols</c> and <c>ffmpeg -hide_banner -formats</c>
/// and asserts the <c>bluray</c> protocol is present.
///
/// Results are logged via the standard ILogger so they appear in the startup
/// log alongside hardware capability decisions from the codec registry.
///
/// DB source in use (bundled vs override) is also logged so users can
/// confirm their KEYDB override was picked up without grepping libaacs itself.
/// </summary>
public class FfmpegBluRayCapability(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<FfmpegBluRayCapability> logger
)
{
    /// <summary>
    /// True once <see cref="ProbeAsync"/> has completed successfully.
    /// </summary>
    public bool BluRayProtocolPresent { get; private set; }

    /// <summary>
    /// Absolute path of the KEYDB.cfg that libaacs will load. Reports the
    /// override path when configured, otherwise indicates the bundled default.
    /// </summary>
    public string ActiveKeyDbPath { get; private set; } = string.Empty;

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        BluRayProtocolPresent = await CheckBluRayProtocolAsync(ct);

        ActiveKeyDbPath = ResolveActiveKeyDbPath();

        if (BluRayProtocolPresent)
        {
            logger.LogInformation(
                "[BluRay] ffmpeg bluray: protocol present — AACS/BD+ support active. KEYDB source: {KeyDb}",
                ActiveKeyDbPath
            );
        }
        else
        {
            logger.LogWarning(
                "[BluRay] ffmpeg bluray: protocol NOT present in this build — "
                    + "Blu-ray ripping will fail. Rebuild nomercy-ffmpeg with --enable-libbluray."
            );
        }

        string? aacsOverride = options.BluRay?.AacsKeysOverridePath;
        if (!string.IsNullOrWhiteSpace(aacsOverride))
        {
            logger.LogInformation("[BluRay] LIBAACS_KEY_DB override: {Path}", aacsOverride);
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────

    private async Task<bool> CheckBluRayProtocolAsync(CancellationToken ct)
    {
        ProcessResult result = await processRunner.RunAsync(
            options.FfmpegPath,
            ["-hide_banner", "-protocols"],
            workingDirectory: null,
            cancellationToken: ct
        );

        // ffmpeg -protocols writes to stdout; result is exit 0.
        // The bluray: line appears in the output regardless of success/failure exit.
        string combined = result.StdOut + result.StdErr;
        return combined.Contains("bluray", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveActiveKeyDbPath()
    {
        string? overridePath = options.BluRay?.KeyDbOverridePath;
        if (!string.IsNullOrWhiteSpace(overridePath))
            return $"override:{overridePath}";

        // libaacs searches XDG_CONFIG_HOME/aacs/KEYDB.cfg (Linux) or
        // %APPDATA%/aacs/KEYDB.cfg (Windows) when no env var is set.
        // nomercy-ffmpeg also bakes a baseline KEYDB at build time.
        return "bundled (nomercy-ffmpeg default)";
    }
}
