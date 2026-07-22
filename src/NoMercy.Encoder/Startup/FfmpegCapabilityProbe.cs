// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;

namespace NoMercy.Encoder.Startup;

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
        logger.LogInformation(message: "Running FFmpeg capability probe");

        // FfmpegCapabilities already probed during HardwareInitializationService —
        // call ProbeAsync only if the sets are empty (e.g. standalone usage).
        if (ffmpegCapabilities.AvailableEncoders.Count == 0)
            await ffmpegCapabilities.ProbeAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);

        bool bluRay = ffmpegCapabilities.HasProtocol(name: "bluray");
        // ffmpeg ≥ 6.1 dropped the dvdread:// protocol form in favour of the
        // dvdvideo demuxer (-f dvdvideo -i /path/to/disc). Both stock builds
        // with --enable-libdvdread and the NoMercy fork expose libdvdread
        // exclusively through that demuxer, so the demuxer's presence is
        // the canonical capability check.
        bool dvdRead = ffmpegCapabilities.HasDemuxer(name: "dvdvideo");

        List<string> missingFilters = RequiredFilters
            .Where(predicate: f => !ffmpegCapabilities.HasFilter(name: f))
            .ToList();

        HashSet<string> availableMuxers = await ProbeMuxersAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);
        List<string> missingMuxers = RequiredMuxers
            .Where(predicate: m => !availableMuxers.Contains(item: m))
            .ToList();

        // Chromaprint fingerprinting is compiled into the NoMercy ffmpeg fork
        // as the chromaprint muxer (-c:a chromaprint) — no separate fpcalc
        // binary. Presence of the muxer is the canonical capability check.
        bool fpcalcPresent = availableMuxers.Contains(item: "chromaprint");

        bool whisperModelPresent = ProbeWhisperModel();

        (bool tesseractPresent, string? tesseractDir) = ProbeTesseract();

        List<EncoderRule> issues = BuildIssues(
            fpcalcPresent: fpcalcPresent,
            whisperModelPresent: whisperModelPresent,
            tesseractPresent: tesseractPresent
        );

        _cached = new(
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
            message: "Capability probe complete — BluRay={BluRay}, DvdRead={DvdRead}, MissingFilters={FilterCount}, MissingMuxers={MuxerCount}, fpcalc={Fpcalc}, WhisperModel={Whisper}, Tesseract={Tesseract}", args: [bluRay, dvdRead, missingFilters.Count, missingMuxers.Count, fpcalcPresent, whisperModelPresent, tesseractPresent]
        );
    }

    // -------------------------------------------------------------------------

    private async Task<HashSet<string>> ProbeMuxersAsync(CancellationToken ct)
    {
        ProcessResult result = await processRunner
            .RunAsync(executable: AppFiles.FfmpegPath, arguments: ["-hide_banner", "-muxers"], workingDirectory: null, cancellationToken: ct)
            .ConfigureAwait(continueOnCapturedContext: false);

        HashSet<string> available = [];
        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.TrimStart();
            // Muxer lines: " E mp4              MP4 (MPEG-4 Part 14)"
            if (trimmed.Length < 3 || trimmed[index: 0] != 'E')
                continue;

            string[] parts = trimmed[1..]
                .TrimStart()
                .Split(separator: ' ', count: 2, options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                available.Add(item: parts[0]);
        }

        return available;
    }

    private bool ProbeWhisperModel()
    {
        string? modelPath = options.WhisperModelPath;
        if (string.IsNullOrWhiteSpace(value: modelPath))
            return false;

        try
        {
            return storage.Exists(path: modelPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private (bool present, string? directory) ProbeTesseract()
    {
        string? dir = options.TesseractModelsDirectory;
        if (string.IsNullOrWhiteSpace(value: dir))
            return (false, null);

        string engPath = Path.Combine(path1: dir, path2: "eng.traineddata");
        try
        {
            return (storage.Exists(path: engPath), dir);
        }
        catch (Exception)
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
                item: new(
                    Id: EncoderRuleId.CapabilityFpcalcMissing,
                    Severity: EncoderRuleSeverity.Warning,
                    Field: "fpcalc",
                    Message: "Chromaprint muxer is not compiled into the bundled ffmpeg fork — audio fingerprinting unavailable.",
                    Fix: "Reinstall NoMercy or rebuild the ffmpeg fork with --enable-chromaprint."
                )
            );

        if (!whisperModelPresent)
            issues.Add(
                item: new(
                    Id: EncoderRuleId.CapabilityWhisperMissing,
                    Severity: EncoderRuleSeverity.Warning,
                    Field: "WhisperModelPath",
                    Message: "Whisper model file is missing or not configured.",
                    Fix: "Set EncoderOptions.WhisperModelPath to a valid ggml .bin model file."
                )
            );

        if (!tesseractPresent)
            issues.Add(
                item: new(
                    Id: EncoderRuleId.CapabilityTesseractModelMissing,
                    Severity: EncoderRuleSeverity.Warning,
                    Field: "TesseractModelsDirectory",
                    Message: "Tesseract eng.traineddata is missing or TesseractModelsDirectory is not configured.",
                    Fix: "Set EncoderOptions.TesseractModelsDirectory to a folder containing eng.traineddata."
                )
            );

        return issues;
    }
}
