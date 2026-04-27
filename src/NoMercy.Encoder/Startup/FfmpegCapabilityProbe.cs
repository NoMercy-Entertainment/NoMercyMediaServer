namespace NoMercy.Encoder.Startup;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

/// <summary>
/// Probes optional runtime dependencies and validates the installed FFmpeg
/// build. Reuses <see cref="IFfmpegCapabilities"/> for filters/protocols so
/// those shell calls are not duplicated. Muxers require a separate probe
/// because <see cref="IFfmpegCapabilities"/> does not expose them.
/// </summary>
public sealed class FfmpegCapabilityProbe(
    IFfmpegCapabilities ffmpegCapabilities,
    IProcessRunner processRunner,
    IStorage storage,
    EncoderOptions options,
    ILogger<FfmpegCapabilityProbe> logger
) : IFfmpegCapabilityProbe
{
    private static readonly string[] RequiredFilters =
    [
        "libplacebo",
        "tonemap_opencl",
        "zscale",
        "cropdetect",
        "ass",
        "overlay",
        "hwupload",
        "hwdownload",
    ];

    private static readonly string[] RequiredMuxers =
    [
        "hls",
        "dash",
        "mp4",
        "matroska",
        "mp3",
        "flac",
        "ogg",
    ];

    private CapabilityReport? _cached;

    public CapabilityReport? GetCachedReport() => _cached;

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Running FFmpeg capability probe");

        // FfmpegCapabilities already probed during HardwareInitializationService —
        // call ProbeAsync only if the sets are empty (e.g. standalone usage).
        if (ffmpegCapabilities.AvailableEncoders.Count == 0)
            await ffmpegCapabilities.ProbeAsync(ct).ConfigureAwait(false);

        bool bluRay = ffmpegCapabilities.HasProtocol("bluray");
        bool dvdRead = ffmpegCapabilities.HasProtocol("dvdread");

        List<string> missingFilters = RequiredFilters
            .Where(f => !ffmpegCapabilities.HasFilter(f))
            .ToList();

        List<string> missingMuxers = await ProbeMissingMuxersAsync(ct).ConfigureAwait(false);

        bool fpcalcPresent = await ProbeExternalToolAsync("fpcalc", "--version", ct)
            .ConfigureAwait(false);

        bool whisperModelPresent = ProbeWhisperModel();

        (bool tesseractPresent, string? tesseractDir) = ProbeTesseract();

        List<EncoderRule> issues = BuildIssues(
            fpcalcPresent,
            whisperModelPresent,
            tesseractPresent
        );

        _cached = new CapabilityReport(
            BluRayProtocol: bluRay,
            DvdReadProtocol: dvdRead,
            AvailableEncoders: ffmpegCapabilities.AvailableEncoders.ToList(),
            MissingFilters: missingFilters,
            MissingMuxers: missingMuxers,
            FpcalcPresent: fpcalcPresent,
            WhisperModelPresent: whisperModelPresent,
            TesseractEngTraineddataPresent: tesseractPresent,
            TesseractModelsDirectory: tesseractDir,
            Issues: issues
        );

        logger.LogInformation(
            "Capability probe complete — BluRay={BluRay}, DvdRead={DvdRead}, "
                + "MissingFilters={FilterCount}, MissingMuxers={MuxerCount}, "
                + "fpcalc={Fpcalc}, WhisperModel={Whisper}, Tesseract={Tesseract}",
            bluRay,
            dvdRead,
            missingFilters.Count,
            missingMuxers.Count,
            fpcalcPresent,
            whisperModelPresent,
            tesseractPresent
        );
    }

    // -------------------------------------------------------------------------

    private async Task<List<string>> ProbeMissingMuxersAsync(CancellationToken ct)
    {
        // Use bundled ffmpeg — see FfmpegCapabilities.ProbeListAsync for context.
        string ffmpegPath = File.Exists(NoMercy.NmSystem.Information.AppFiles.FfmpegPath)
            ? NoMercy.NmSystem.Information.AppFiles.FfmpegPath
            : "ffmpeg";
        ProcessResult result = await processRunner
            .RunAsync(ffmpegPath, ["-hide_banner", "-muxers"], null, ct)
            .ConfigureAwait(false);

        HashSet<string> available = [];
        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.TrimStart();
            // Muxer lines: " E mp4              MP4 (MPEG-4 Part 14)"
            if (trimmed.Length < 3 || trimmed[0] != 'E')
                continue;

            string[] parts = trimmed[1..]
                .TrimStart()
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                available.Add(parts[0]);
        }

        return RequiredMuxers.Where(m => !available.Contains(m)).ToList();
    }

    private async Task<bool> ProbeExternalToolAsync(
        string executable,
        string versionArg,
        CancellationToken ct
    )
    {
        try
        {
            ProcessResult result = await processRunner
                .RunAsync(executable, [versionArg], null, ct)
                .ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "External tool '{Executable}' not found or failed version check",
                executable
            );
            return false;
        }
    }

    private bool ProbeWhisperModel()
    {
        string? modelPath = options.WhisperModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
            return false;

        try
        {
            return storage.Exists(modelPath);
        }
        catch
        {
            return false;
        }
    }

    private (bool present, string? directory) ProbeTesseract()
    {
        string? dir = options.TesseractModelsDirectory;
        if (string.IsNullOrWhiteSpace(dir))
            return (false, null);

        string engPath = Path.Combine(dir, "eng.traineddata");
        try
        {
            return (storage.Exists(engPath), dir);
        }
        catch
        {
            return (false, dir);
        }
    }

    private static List<EncoderRule> BuildIssues(
        bool fpcalcPresent,
        bool whisperModelPresent,
        bool tesseractPresent
    )
    {
        List<EncoderRule> issues = [];

        if (!fpcalcPresent)
            issues.Add(
                new EncoderRule(
                    EncoderRuleId.CapabilityFpcalcMissing,
                    EncoderRuleSeverity.Warning,
                    "fpcalc",
                    "fpcalc (chromaprint) is not installed or not on PATH.",
                    "Install chromaprint: 'apt install libchromaprint-tools' / 'brew install chromaprint' / download from acoustid.org/chromaprint."
                )
            );

        if (!whisperModelPresent)
            issues.Add(
                new EncoderRule(
                    EncoderRuleId.CapabilityWhisperMissing,
                    EncoderRuleSeverity.Warning,
                    "WhisperModelPath",
                    "Whisper model file is missing or not configured.",
                    "Set EncoderOptions.WhisperModelPath to a valid ggml .bin model file."
                )
            );

        if (!tesseractPresent)
            issues.Add(
                new EncoderRule(
                    EncoderRuleId.CapabilityTesseractModelMissing,
                    EncoderRuleSeverity.Warning,
                    "TesseractModelsDirectory",
                    "Tesseract eng.traineddata is missing or TesseractModelsDirectory is not configured.",
                    "Set EncoderOptions.TesseractModelsDirectory to a folder containing eng.traineddata."
                )
            );

        return issues;
    }
}
