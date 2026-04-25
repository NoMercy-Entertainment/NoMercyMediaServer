namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

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
/// </summary>
public class ChromaprintFingerprinter(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    ILogger<ChromaprintFingerprinter> logger,
    IAnalysisProgressObserver? progress = null
) : IAudioFingerprinter
{
    // chromaprint advances one fingerprint frame every 2048 samples at
    // 11025 Hz = ~0.18576 seconds per frame.
    private static readonly TimeSpan ChromaprintFrameDuration = TimeSpan.FromSeconds(
        2048.0 / 11025.0
    );

    public async Task<AudioFingerprint> FingerprintAsync(
        string filePath,
        FingerprintWindow? window,
        CancellationToken ct
    )
    {
        string jobId = Guid.NewGuid().ToString("N");
        IAnalysisProgressObserver observer = progress ?? NullAnalysisProgressObserver.Instance;
        observer.Report(jobId, "fingerprint", 0, "fingerprinting");

        await using LocalPathLease inputLease = storage.AcquireLocalPath(filePath);
        List<string> args = ["-v", "error", "-nostdin"];

        if (window is not null)
        {
            args.Add("-ss");
            args.Add(window.Start.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
            args.Add("-t");
            args.Add(window.Duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // Drop video, downmix to mono at chromaprint's preferred sample rate.
        args.AddRange([
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
            options.FfmpegPath,
            args.ToArray(),
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "chromaprint fingerprinting failed for {Path} (exit {Exit}): {Stderr}",
                filePath,
                result.ExitCode,
                result.StdErr
            );
            observer.Report(jobId, "fingerprint", 100, "failed");
            return Empty(window);
        }

        uint[] hashes = ParseRawOutput(result.StdOut);
        TimeSpan startTime = window?.Start ?? TimeSpan.Zero;

        observer.Report(jobId, "fingerprint", 100, "done");
        return new(hashes, ChromaprintFrameDuration, startTime);
    }

    /// <summary>
    /// chromaprint raw format: a decimal representation of unsigned 32-bit
    /// integers separated by commas (the muxer writes them as text despite
    /// the "raw" label — it skips the base64 encoding step, not the text
    /// formatting step). Example: <c>"1234,5678,9012\n"</c>.
    /// </summary>
    internal static uint[] ParseRawOutput(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return [];

        // Some ffmpeg builds prefix the line with a label like "CHROMAPRINT=".
        int eq = stdout.IndexOf('=');
        string payload = eq >= 0 ? stdout[(eq + 1)..] : stdout;

        List<uint> hashes = [];
        StringBuilder token = new();

        foreach (char c in payload)
        {
            if (char.IsDigit(c) || c == '-')
            {
                token.Append(c);
                continue;
            }

            if (token.Length > 0)
            {
                AppendToken(token, hashes);
                token.Clear();
            }
        }

        if (token.Length > 0)
            AppendToken(token, hashes);

        return hashes.ToArray();
    }

    private static void AppendToken(StringBuilder token, List<uint> hashes)
    {
        string s = token.ToString();
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signed))
        {
            hashes.Add(unchecked((uint)signed));
        }
    }

    private static AudioFingerprint Empty(FingerprintWindow? window) =>
        new([], ChromaprintFrameDuration, window?.Start ?? TimeSpan.Zero);
}
