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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.Encoder.ContentAnalysis;

/// <inheritdoc />
public class EncodeYieldProbe(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<EncodeYieldProbe> logger
) : IEncodeYieldProbe
{
    // Sampled a quarter of the way in and 30s long. An anime opening or a cold
    // open is the least representative stretch of a file — bright, high-motion,
    // and far more expensive than the body of the episode — so starting there
    // would overstate the yield and argue against re-encodes that are worth it.
    private const double StartFraction = 0.25;
    private static readonly TimeSpan SampleDuration = TimeSpan.FromSeconds(30);

    // Below this the sample is too short for the measurement to mean anything.
    private static readonly TimeSpan MinimumSourceDuration = TimeSpan.FromSeconds(60);

    private static readonly string[] HardwareEncoderMarkers =
    [
        "nvenc",
        "qsv",
        "amf",
        "vaapi",
        "videotoolbox",
    ];

    private static readonly Dictionary<VideoCodecType, string> SoftwareEncoders = new()
    {
        [VideoCodecType.H264] = "libx264",
        [VideoCodecType.H265] = "libx265",
        [VideoCodecType.Av1] = "libsvtav1",
        [VideoCodecType.Vp9] = "libvpx-vp9",
    };

    public async Task<long?> EstimateBitrateKbpsAsync(
        string inputPath,
        EncodeYieldTarget target,
        TimeSpan sourceDuration,
        CancellationToken ct
    )
    {
        if (sourceDuration < MinimumSourceDuration)
            return null;

        string? encoder = target.EncoderName;
        if (string.IsNullOrWhiteSpace(encoder) && !SoftwareEncoders.TryGetValue(target.Codec, out encoder))
            return null;

        // Hardware encoders take the quality target on their own flag and reject
        // the software preset and tune names outright, so the sample is built for
        // whichever family is measuring. Getting this wrong does not misprice the
        // encode, it fails the probe and silently falls back to copying.
        bool isHardware = HardwareEncoderMarkers.Any(marker =>
            encoder!.Contains(marker, StringComparison.OrdinalIgnoreCase)
        );

        string samplePath = Path.Combine(
            Path.GetTempPath(),
            $"nomercy-yield-{Guid.NewGuid():N}.mkv"
        );

        try
        {
            await using LocalPathLease inputLease = storage.AcquireLocalPath(inputPath);

            int startSeconds = (int)(sourceDuration.TotalSeconds * StartFraction);

            List<string> args =
            [
                "-hide_banner",
                "-v",
                "error",
                "-y",
                "-ss",
                startSeconds.ToString(),
                "-t",
                ((int)SampleDuration.TotalSeconds).ToString(),
                "-i",
                inputLease.Path,
                "-map",
                "0:v:0",
                "-an",
                "-sn",
                "-dn",
                "-c:v",
                encoder!,
                isHardware ? "-cq" : "-crf",
                target.Crf.ToString(),
            ];

            if (!isHardware && !string.IsNullOrWhiteSpace(target.Preset))
            {
                args.Add("-preset");
                args.Add(target.Preset);
            }

            if (!isHardware && !string.IsNullOrWhiteSpace(target.Tune))
            {
                args.Add("-tune");
                args.Add(target.Tune);
            }

            if (!string.IsNullOrWhiteSpace(target.PixelFormat))
            {
                args.Add("-pix_fmt");
                args.Add(target.PixelFormat);
            }

            args.Add(samplePath);

            ProcessResult result = await processRunner
                .RunAsync(
                    options.FfmpegPath,
                    args.ToArray(),
                    workingDirectory: null,
                    cancellationToken: ct
                )
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                logger.LogDebug(
                    "Encode-yield probe exited {ExitCode} for {Input}; yield unknown",
                    result.ExitCode,
                    inputPath
                );
                return null;
            }

            FileInfo sample = new(samplePath);
            if (!sample.Exists || sample.Length == 0)
                return null;

            long kbps = (long)(sample.Length * 8 / SampleDuration.TotalSeconds / 1000);

            logger.LogInformation(
                "Encode-yield probe: {Encoder} CRF {Crf} yields ~{Kbps} kbps on {Input}",
                encoder,
                target.Crf,
                kbps,
                Path.GetFileName(inputPath)
            );

            return kbps;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Encode-yield probe failed for {Input}; yield unknown", inputPath);
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(samplePath))
                    File.Delete(samplePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
