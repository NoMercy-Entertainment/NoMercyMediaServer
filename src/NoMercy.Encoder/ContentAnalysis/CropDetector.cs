namespace NoMercy.Encoder.ContentAnalysis;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;

/// <summary>
/// Detects black-bar cropping by running FFmpeg's <c>cropdetect</c> filter on a
/// short sample near the middle of the input and picking the most frequent
/// <c>crop=W:H:X:Y</c> line from stderr.
///
/// The middle of the file is sampled (not the opening logo or credits) so a
/// single black frame at the start does not skew the detection.
/// </summary>
public partial class CropDetector(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<CropDetector> logger
) : ICropDetector
{
    // Minimum number of observations before we trust a crop value. Protects
    // against one-off black frames producing spurious crops.
    private const int MinObservations = 5;

    // Maximum scan window — we don't need to decode the whole file.
    private static readonly TimeSpan ScanDuration = TimeSpan.FromSeconds(60);

    public async Task<CropResult> DetectAsync(string inputPath, CancellationToken ct)
    {
        string[] args =
        [
            "-hide_banner",
            "-ss",
            "120", // skip intro/logos
            "-i",
            inputPath,
            "-t",
            ((int)ScanDuration.TotalSeconds).ToString(),
            "-vf",
            "cropdetect=limit=24:round=2:reset=0",
            "-f",
            "null",
            "-",
        ];

        Dictionary<string, int> observations = [];

        ProcessResult result = await processRunner.RunAsync(
            options.FfmpegPath,
            args,
            onStdOut: null,
            onStdErr: line =>
            {
                Match match = CropRegex().Match(line);
                if (!match.Success)
                    return;

                string crop = match.Groups["crop"].Value;
                observations[crop] = observations.GetValueOrDefault(crop) + 1;
            },
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "cropdetect returned exit code {ExitCode}. Skipping crop.",
                result.ExitCode
            );
            return new(0, 0, 0, 0, ShouldCrop: false);
        }

        if (observations.Count == 0)
        {
            logger.LogDebug("cropdetect observed no crop values for {Input}", inputPath);
            return new(0, 0, 0, 0, ShouldCrop: false);
        }

        (string bestCrop, int count) = observations.OrderByDescending(kv => kv.Value).First()
            is var first
            ? (first.Key, first.Value)
            : ("", 0);

        if (count < MinObservations)
        {
            logger.LogDebug(
                "cropdetect top result {Crop} has only {Count} observations; not cropping",
                bestCrop,
                count
            );
            return new(0, 0, 0, 0, ShouldCrop: false);
        }

        // crop=W:H:X:Y — parse the four parts.
        string[] parts = bestCrop.Split(':');
        if (parts.Length != 4)
            return new(0, 0, 0, 0, ShouldCrop: false);

        if (
            !int.TryParse(parts[0], out int width)
            || !int.TryParse(parts[1], out int height)
            || !int.TryParse(parts[2], out int x)
            || !int.TryParse(parts[3], out int y)
        )
        {
            return new(0, 0, 0, 0, ShouldCrop: false);
        }

        logger.LogInformation(
            "cropdetect → {Width}x{Height} at ({X},{Y}) ({Count} obs)",
            width,
            height,
            x,
            y,
            count
        );

        // Do not crop when the detected rectangle matches the full frame.
        bool shouldCrop = x > 0 || y > 0;

        return new(width, height, x, y, shouldCrop);
    }

    [GeneratedRegex(@"crop=(?<crop>\d+:\d+:\d+:\d+)")]
    private static partial Regex CropRegex();
}
