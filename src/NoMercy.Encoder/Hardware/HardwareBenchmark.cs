namespace NoMercy.Encoder.Hardware;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Infrastructure;

/// <summary>
/// Runs short calibration encodes to populate <see cref="SpeedIndex"/>.
/// For each available encoder × resolution tier it spawns a 5-second
/// synthetic source via the lavfi <c>testsrc</c> muxer and measures the
/// achieved fps from ffmpeg's structured progress output. Results persist
/// via <see cref="ISpeedIndexStore"/> so subsequent server starts avoid
/// the cost — see <see cref="NeedsRecalibration"/>.
/// </summary>
public class HardwareBenchmark(
    CodecRegistry codecRegistry,
    IHardwareCapabilities hardware,
    IProcessRunner processRunner,
    ISpeedIndexStore store,
    EncoderOptions options,
    ILogger<HardwareBenchmark> logger
) : IHardwareBenchmark
{
    // Standard benchmark tiers. 2160p reserved for when we know the hardware
    // can sustain it — otherwise the test becomes a 30-second ordeal on slow
    // boxes. 4K calibration lands in a follow-up.
    internal static readonly (int Width, int Height)[] DefaultTiers =
    [
        (1920, 1080),
        (1280, 720),
        (854, 480),
    ];

    private const double CalibrationDurationSeconds = 5.0;
    private const double SourceFrameRate = 30.0;

    // Recalibrate once a month by default. Hardware / driver updates can
    // change real throughput noticeably.
    private static readonly TimeSpan RecalibrationInterval = TimeSpan.FromDays(30);

    private SpeedIndex _cache = new(new Dictionary<SpeedKey, SpeedMeasurement>());

    public SpeedIndex GetCachedIndex()
    {
        if (_cache.Measurements.Count > 0)
            return _cache;

        SpeedIndex? loaded = store.Load();
        if (loaded is not null)
        {
            _cache = loaded;
            return loaded;
        }

        return _cache;
    }

    public bool NeedsRecalibration()
    {
        SpeedIndex cached = GetCachedIndex();
        if (cached.Measurements.Count == 0)
            return true;

        DateTime? lastCalibrated = store.LastCalibratedAt;
        if (lastCalibrated is null)
            return true;

        return DateTime.UtcNow - lastCalibrated.Value > RecalibrationInterval;
    }

    public async Task<SpeedIndex> CalibrateAsync(CancellationToken ct)
    {
        Dictionary<SpeedKey, SpeedMeasurement> results = new();

        foreach ((VideoCodecType codecType, EncoderInfo encoder) in SelectCandidates())
        {
            foreach ((int tierWidth, int tierHeight) in DefaultTiers)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    SpeedMeasurement? measured = await MeasureAsync(
                        encoder,
                        tierWidth,
                        tierHeight,
                        ct
                    );

                    if (measured is null)
                        continue;

                    string? deviceName = ResolveDeviceName(encoder);
                    SpeedKey key = new(codecType, encoder.FfmpegName, tierWidth, deviceName);
                    results[key] = measured;

                    logger.LogInformation(
                        "Benchmarked {Encoder} @ {W}x{H}: {Fps:F1} fps ({Speed:F2}x)",
                        encoder.FfmpegName,
                        tierWidth,
                        tierHeight,
                        measured.Fps,
                        measured.SpeedMultiplier
                    );
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Benchmark failed for {Encoder} @ {W}x{H} — skipping",
                        encoder.FfmpegName,
                        tierWidth,
                        tierHeight
                    );
                }
            }
        }

        SpeedIndex index = new(results);
        store.Save(index);
        _cache = index;
        return index;
    }

    internal IEnumerable<(VideoCodecType Codec, EncoderInfo Encoder)> SelectCandidates()
    {
        foreach (
            (VideoCodecType codec, EncoderInfo encoder) in codecRegistry.EnumerateVideoEncoders()
        )
        {
            // Skip hardware encoders for vendors we don't have — avoids
            // ffmpeg bailing out with "device not found".
            if (encoder.RequiredVendor is GpuVendor vendor)
            {
                bool hasVendor = hardware.Gpus.Any(g => g.Vendor == vendor);
                if (!hasVendor)
                    continue;
            }

            yield return (codec, encoder);
        }
    }

    private async Task<SpeedMeasurement?> MeasureAsync(
        EncoderInfo encoder,
        int width,
        int height,
        CancellationToken ct
    )
    {
        string[] arguments = BuildCalibrationArguments(encoder, width, height);

        ProgressParser parser = new();
        double observedFps = 0;

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = parser.FeedLine(line);
            if (snapshot is null)
                return;

            if (snapshot.Fps > 0)
                observedFps = Math.Max(observedFps, snapshot.Fps);
        }

        ProcessResult result = await processRunner.RunAsync(
            options.FfmpegPath,
            arguments,
            OnStdOut,
            null,
            null,
            ct
        );

        if (!result.IsSuccess || observedFps <= 0)
            return null;

        double multiplier = observedFps / SourceFrameRate;
        return new SpeedMeasurement(
            Fps: observedFps,
            SpeedMultiplier: multiplier,
            MeasuredAt: DateTime.UtcNow
        );
    }

    internal static string[] BuildCalibrationArguments(EncoderInfo encoder, int width, int height)
    {
        List<string> args =
        [
            "-hide_banner",
            "-nostats",
            "-loglevel",
            "error",
            "-f",
            "lavfi",
            "-i",
            $"testsrc=duration={CalibrationDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}:size={width}x{height}:rate={SourceFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "-c:v",
            encoder.FfmpegName,
        ];

        // Vendor-specific flags (e.g. -usage transcoding for AMF).
        foreach ((string flag, string value) in encoder.VendorSpecificFlags)
        {
            args.Add(flag);
            args.Add(value);
        }

        // Use a reasonable default preset if the encoder has one. Matches
        // what production encodes would typically do.
        if (encoder.Presets.Length > 0)
        {
            string preset =
                encoder.Presets.Contains("medium") ? "medium"
                : encoder.Presets.Contains("p4") ? "p4"
                : encoder.Presets[encoder.Presets.Length / 2];
            args.Add("-preset");
            args.Add(preset);
        }

        args.Add("-f");
        args.Add("null");
        args.Add("-");
        args.Add("-progress");
        args.Add("pipe:1");

        return [.. args];
    }

    private string? ResolveDeviceName(EncoderInfo encoder)
    {
        if (encoder.RequiredVendor is not GpuVendor vendor)
            return null;

        GpuDevice? device = hardware.Gpus.FirstOrDefault(g => g.Vendor == vendor);
        return device?.Name;
    }
}
