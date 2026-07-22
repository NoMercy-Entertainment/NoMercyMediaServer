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
using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;
using NoMercy.NmSystem.Monitoring;
using NoMercy.Storage;

namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// FFmpeg-backed fingerprinter. Invokes ffmpeg with the chromaprint muxer
/// and parses its output. Uses the <c>text</c> format which emits one
/// decimal hash per line — easier to parse and less brittle than <c>raw</c>
/// (raw is big-endian and has an implicit frame count we'd have to reverse).
///
/// chromaprint's documented algorithm advances one hash per ~0.1858 s at
/// its internal 11025 Hz mono rate. Users rarely need sub-second marker
/// precision for intros, so we treat FrameDuration as a constant rather
/// than reading it back from ffmpeg.
///
/// This is the fire-and-forget path both <c>IntroDetectSubscriber</c> and
/// <c>IntroDetectionSubscriber</c> funnel through, and the worst NAS-read
/// offender among background jobs — each call reads up to a few minutes of
/// audio per episode. It yields to active playback via
/// <see cref="MediaActivityMonitor.WaitForIdleAsync"/> before touching the
/// source file, so it never competes with a streaming segment serve for the
/// same NFS mount.
/// </summary>
public class ChromaprintFingerprinter(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<ChromaprintFingerprinter> logger,
    MediaActivityMonitor activityMonitor,
    IAnalysisProgressObserver? progress = null
) : IAudioFingerprinter
{
    // chromaprint advances one fingerprint frame every 2048 samples at
    // 11025 Hz = ~0.18576 seconds per frame.
    private static readonly TimeSpan ChromaprintFrameDuration = TimeSpan.FromSeconds(
        value: 2048.0 / 11025.0
    );

    private static readonly TimeSpan MaxIdleWait = TimeSpan.FromMinutes(minutes: 5);

    public async Task<AudioFingerprint> FingerprintAsync(
        string filePath,
        FingerprintWindow? window,
        CancellationToken ct
    )
    {
        string jobId = Guid.NewGuid().ToString(format: "N");
        IAnalysisProgressObserver observer = progress ?? NullAnalysisProgressObserver.Instance;
        observer.Report(jobId: jobId, type: "fingerprint", percent: 0, stage: "fingerprinting");

        // Defer to active playback (never longer than MaxIdleWait) before
        // reading from the source file — see MediaActivityMonitor.
        await activityMonitor.WaitForIdleAsync(maxWait: MaxIdleWait, ct: ct).ConfigureAwait(continueOnCapturedContext: false);

        await using LocalPathLease inputLease = storage.AcquireLocalPath(path: filePath);
        List<string> args = ["-v", "error", "-nostdin"];

        if (window is not null)
        {
            double startSeconds = window.Start.TotalSeconds;

            // A negative Start means "relative to the end of the file" (e.g. the
            // outro window). -ss only accepts a non-negative offset from the
            // start — the end-relative case needs -sseof, which takes the same
            // negative value ffmpeg subtracts from the input duration.
            if (startSeconds < 0)
            {
                args.Add(item: "-sseof");
                args.Add(item: startSeconds.ToString(format: "F3", provider: CultureInfo.InvariantCulture));
            }
            else
            {
                args.Add(item: "-ss");
                args.Add(item: startSeconds.ToString(format: "F3", provider: CultureInfo.InvariantCulture));
            }

            args.Add(item: "-t");
            args.Add(item: window.Duration.TotalSeconds.ToString(format: "F3", provider: CultureInfo.InvariantCulture));
        }

        // Drop video, downmix to mono at chromaprint's preferred sample rate.
        args.AddRange(collection:
        [
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
            "raw",
            "-",
        ]);

        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfmpegPath,
            arguments: args.ToArray(),
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                message: "chromaprint fingerprinting failed for {Path} (exit {Exit}): {Stderr}", args: [filePath, result.ExitCode, result.StdErr]
            );
            observer.Report(jobId: jobId, type: "fingerprint", percent: 100, stage: "failed");
            return Empty(window: window);
        }

        uint[] hashes = ParseRawOutput(stdout: result.StdOut);
        TimeSpan startTime = window?.Start ?? TimeSpan.Zero;

        observer.Report(jobId: jobId, type: "fingerprint", percent: 100, stage: "done");
        return new(Hashes: hashes, FrameDuration: ChromaprintFrameDuration, StartTime: startTime);
    }

    /// <summary>
    /// chromaprint raw format: a decimal representation of unsigned 32-bit
    /// integers separated by commas (the muxer writes them as text despite
    /// the "raw" label — it skips the base64 encoding step, not the text
    /// formatting step). Example: <c>"1234,5678,9012\n"</c>.
    /// </summary>
    internal static uint[] ParseRawOutput(string stdout)
    {
        if (string.IsNullOrWhiteSpace(value: stdout))
            return [];

        // Some ffmpeg builds prefix the line with a label like "CHROMAPRINT=".
        int eq = stdout.IndexOf(value: '=');
        string payload = eq >= 0 ? stdout[(eq + 1)..] : stdout;

        List<uint> hashes = [];
        StringBuilder token = new();

        foreach (char c in payload)
        {
            if (char.IsDigit(c: c) || c == '-')
            {
                token.Append(value: c);
                continue;
            }

            if (token.Length > 0)
            {
                AppendToken(token: token, hashes: hashes);
                token.Clear();
            }
        }

        if (token.Length > 0)
            AppendToken(token: token, hashes: hashes);

        return hashes.ToArray();
    }

    private static void AppendToken(StringBuilder token, List<uint> hashes)
    {
        string s = token.ToString();
        if (int.TryParse(s: s, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out int signed))
        {
            hashes.Add(item: unchecked((uint)signed));
        }
    }

    private static AudioFingerprint Empty(FingerprintWindow? window) =>
        new(Hashes: [], FrameDuration: ChromaprintFrameDuration, StartTime: window?.Start ?? TimeSpan.Zero);
}
