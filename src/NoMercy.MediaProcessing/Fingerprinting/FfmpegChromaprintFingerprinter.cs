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

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Providers.AcoustId;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Fingerprinting;

/// <summary>
/// <see cref="IAudioFingerprinter"/> for the AcoustID lookup path — the compressed
/// chromaprint string via the fork ffmpeg's built-in chromaprint muxer (no separate
/// fpcalc binary). Lives in MediaProcessing because Providers must not reference the
/// Encoder-layer process runner and ffmpeg path it needs.
/// </summary>
public sealed partial class FfmpegChromaprintFingerprinter(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<FfmpegChromaprintFingerprinter> logger
) : IAudioFingerprinter
{
    [GeneratedRegex(pattern: @"DURATION=([0-9]+(?:\.[0-9]+)?)", options: RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    [GeneratedRegex(pattern: @"FINGERPRINT=([A-Za-z0-9_-]+)", options: RegexOptions.IgnoreCase)]
    private static partial Regex FingerprintRegex();

    public async Task<AudioFingerprint?> FingerprintAsync(string filePath, CancellationToken ct)
    {
        await using LocalPathLease inputLease = storage.AcquireLocalPath(path: filePath);

        string[] arguments =
        [
            "-v",
            "error",
            "-nostdin",
            "-i",
            inputLease.Path,
            "-vn",
            "-sn",
            "-dn",
            "-ac",
            "1",
            "-ar",
            "11025",
            "-f",
            "chromaprint",
            "-fp_format",
            "compressed",
            "-",
        ];

        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfmpegPath,
            arguments: arguments,
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                message: "chromaprint fingerprinting failed for {Path} (exit {Exit}): {Stderr}", args: [filePath, result.ExitCode, result.StdErr]
            );
            return null;
        }

        string output = result.StdOut;
        Match fingerprintMatch = FingerprintRegex().Match(input: output);
        if (!fingerprintMatch.Success)
        {
            logger.LogWarning(
                message: "chromaprint produced no FINGERPRINT for {Path}; output: {Output}", args: [filePath, Truncate(value: output, max: 200)]
            );
            return null;
        }

        int durationSeconds = 0;
        Match durationMatch = DurationRegex().Match(input: output);
        if (
            durationMatch.Success
            && double.TryParse(
                s: durationMatch.Groups[groupnum: 1].Value,
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double parsedDuration
            )
        )
            durationSeconds = (int)Math.Round(a: parsedDuration);

        return new(Fingerprint: fingerprintMatch.Groups[groupnum: 1].Value, DurationSeconds: durationSeconds);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
