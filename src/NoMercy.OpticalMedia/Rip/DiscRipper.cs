using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;
using NoMercy.Storage;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Rips optical-disc titles to intermediate MKV files on disk. Each title
/// becomes <c>{outputDir}/title_{index}.mkv</c> via a stream-copy FFmpeg
/// command — no re-encode at rip time, that happens in a follow-up
/// <c>VideoEncodeJob</c> once the MKV is on disk.
///
/// Stream copy is the right default here: disc codecs (H.264 / HEVC video,
/// AC3 / DTS / TrueHD audio, PGS / VobSub subtitles) all pass straight
/// through to the MKV container so the rip preserves source quality
/// perfectly. Re-encoding happens later with the user's chosen profile.
/// </summary>
public class DiscRipper(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    DriveLockRegistry driveLockRegistry,
    ILogger<DiscRipper> logger
) : IDiscRipper
{
    public async Task<DiscRipResult[]> RipAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string lockKey = string.IsNullOrWhiteSpace(request.VolumeUuid)
            ? request.DrivePath
            : request.VolumeUuid;

        if (!driveLockRegistry.TryAcquire(lockKey, out DriveLock? driveLock))
        {
            throw new DiscDriveBusyException(lockKey);
        }

        try
        {
            return await RipInternalAsync(request, outputDirectory, ct);
        }
        finally
        {
            driveLock!.Dispose();
        }
    }

    private async Task<DiscRipResult[]> RipInternalAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    )
    {
        storage.CreateDirectory(outputDirectory);

        List<DiscRipResult> results = [];
        foreach (int titleIndex in request.SelectedTitleIndices)
        {
            ct.ThrowIfCancellationRequested();
            DiscRipResult result = await RipOneTitleAsync(request, titleIndex, outputDirectory, ct);
            results.Add(result);
            if (!result.Success)
                logger.LogWarning(
                    "Rip title {Index} from {Drive} failed: {Error}",
                    titleIndex,
                    request.DrivePath,
                    result.Error
                );
        }
        return results.ToArray();
    }

    private async Task<DiscRipResult> RipOneTitleAsync(
        RipRequest request,
        int titleIndex,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string outputPath = Path.Combine(outputDirectory, $"title_{titleIndex:D2}.mkv");

        OpticalDiscType discType = ResolveDiscType(request);
        string inputUrl = BuildInputUrl(request.DrivePath, discType, titleIndex);

        List<string> args = ["-y", "-hide_banner"];

        // Per-disc-type input flags. Bluray uses the bluray: protocol with
        // -playlist; DVD needs -f dvdvideo + -title since libdvdread doesn't
        // accept paths via the protocol layer; CD reads via libcdio.
        switch (discType)
        {
            case OpticalDiscType.BluRay:
                args.Add("-playlist");
                args.Add(titleIndex.ToString());
                break;
            case OpticalDiscType.Dvd:
                args.Add("-f");
                args.Add("dvdvideo");
                args.Add("-title");
                args.Add(titleIndex.ToString());
                break;
            case OpticalDiscType.Cd:
                args.Add("-f");
                args.Add("libcdio");
                break;
        }

        args.Add("-i");
        args.Add(inputUrl);

        // Map only the audio / subtitle streams the user opted into.
        args.Add("-map");
        args.Add("0:v:0");

        foreach (AudioTrackSelection audio in request.AudioTracks.Where(a => a.Include))
        {
            args.Add("-map");
            args.Add($"0:a:{audio.StreamIndex}");
        }

        foreach (SubtitleSelection sub in request.Subtitles.Where(s => s.Include))
        {
            args.Add("-map");
            args.Add($"0:s:{sub.StreamIndex}");
        }

        // Stream copy — preserves disc-source quality and finishes in
        // read-speed rather than encode-speed time.
        args.Add("-c");
        args.Add("copy");

        // Lease the output path through IStorage so remote drivers can
        // stage it locally for ffmpeg and clean up on dispose.
        await using LocalPathLease outputLease = storage.AcquireLocalPath(outputPath);
        args.Add(outputLease.Path);

        // Forward AACS / BD+ database overrides into the child process environment
        // when the caller has configured them.  The host process environment is
        // never mutated — we pass a separate dict to the process runner.
        //
        // Variable names sourced from libaacs src/libaacs/aacs.c (LIBAACS_KEY_DB)
        // and libbdplus src/libbdplus/bdplus.c (LIBBDPLUS_DATABASE).
        // Assumption: names are stable; documented here in case a future upstream
        // release renames them.
        Dictionary<string, string>? envOverrides = BuildBluRayEnvOverrides(request.DrivePath);

        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessResult result = envOverrides is { Count: > 0 }
            ? await processRunner.RunAsync(
                options.FfmpegPath,
                args.ToArray(),
                envOverrides,
                workingDirectory: outputDirectory,
                cancellationToken: ct
            )
            : await processRunner.RunAsync(
                options.FfmpegPath,
                args.ToArray(),
                workingDirectory: outputDirectory,
                cancellationToken: ct
            );
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            // Surface structured AACS / BD+ errors when the rip fails mid-stream.
            if (request.DrivePath.StartsWith("bluray:", StringComparison.OrdinalIgnoreCase))
            {
                DiscScanner.ClassifyBluRayStderr(request.DrivePath, result.StdErr);
            }

            string stderrTail =
                string.IsNullOrEmpty(result.StdErr) ? "(no stderr)"
                : result.StdErr.Length > 800 ? result.StdErr[^800..]
                : result.StdErr;
            logger.LogInformation(
                "ffmpeg rip failed exit={Exit} args=[{Args}] stderr_tail={Stderr}",
                result.ExitCode,
                string.Join(" ", args),
                stderrTail
            );

            return new(
                TitleIndex: titleIndex,
                OutputPath: outputPath,
                Success: false,
                Duration: stopwatch.Elapsed,
                OutputSizeBytes: 0,
                Error: $"ffmpeg exited with code {result.ExitCode}"
            );
        }

        long size = storage.SizeOrZero(outputPath);
        logger.LogInformation(
            "Ripped title {Index} from {Drive} → {Path} ({Bytes} bytes, {Duration:c})",
            titleIndex,
            request.DrivePath,
            outputPath,
            size,
            stopwatch.Elapsed
        );

        return new(
            TitleIndex: titleIndex,
            OutputPath: outputPath,
            Success: true,
            Duration: stopwatch.Elapsed,
            OutputSizeBytes: size,
            Error: null
        );
    }

    /// <summary>
    /// Resolves the disc type the rip should use. Prefers the
    /// <see cref="RipRequest.DiscType"/> the caller populated; falls back
    /// to sniffing the drive-path prefix for raw API calls that don't
    /// supply it.
    /// </summary>
    private static OpticalDiscType ResolveDiscType(RipRequest request)
    {
        if (request.DiscType != OpticalDiscType.None)
            return request.DiscType;
        if (request.DrivePath.StartsWith("bluray:", StringComparison.OrdinalIgnoreCase))
            return OpticalDiscType.BluRay;
        if (request.DrivePath.StartsWith("dvd:", StringComparison.OrdinalIgnoreCase))
            return OpticalDiscType.Dvd;
        return OpticalDiscType.None;
    }

    /// <summary>
    /// Builds the ffmpeg <c>-i</c> URL for the disc + title. Bluray expects
    /// <c>bluray:&lt;mount&gt;/</c> with a trailing separator; DVD points at
    /// the VIDEO_TS folder so libdvdread's filesystem fallback works on
    /// virtual / USB drives that don't expose SCSI MMC; CD passes the raw
    /// device path to libcdio.
    /// </summary>
    private static string BuildInputUrl(string drivePath, OpticalDiscType discType, int titleIndex)
    {
        if (drivePath.StartsWith("bluray:", StringComparison.OrdinalIgnoreCase))
            return drivePath;
        if (drivePath.StartsWith("dvd:", StringComparison.OrdinalIgnoreCase))
            return drivePath;

        string trimmed = drivePath.TrimEnd('\\', '/');
        return discType switch
        {
            OpticalDiscType.BluRay => $"bluray:{trimmed}/",
            OpticalDiscType.Dvd => $"{trimmed}/VIDEO_TS/",
            OpticalDiscType.Cd => drivePath,
            _ => drivePath,
        };
    }

    /// <summary>
    /// Builds the environment-variable overrides to pass to the ffmpeg child
    /// process for Blu-ray disc paths. Returns null / empty when no overrides
    /// are configured (DVD paths also return null — they don't use libaacs).
    /// </summary>
    private Dictionary<string, string>? BuildBluRayEnvOverrides(string drivePath)
    {
        if (!drivePath.StartsWith("bluray:", StringComparison.OrdinalIgnoreCase))
            return null;

        BluRayOptions? bluRay = options.BluRay;
        if (bluRay is null)
            return null;

        Dictionary<string, string> env = [];

        if (!string.IsNullOrWhiteSpace(bluRay.KeyDbOverridePath))
            env["LIBAACS_KEY_DB"] = bluRay.KeyDbOverridePath;

        if (!string.IsNullOrWhiteSpace(bluRay.AacsKeysOverridePath))
            env["LIBBDPLUS_DATABASE"] = bluRay.AacsKeysOverridePath;

        return env.Count > 0 ? env : null;
    }
}
