namespace NoMercy.Encoder.Hardware;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;

/// <summary>
/// Runs short calibration encodes to populate <see cref="SpeedIndex"/>.
/// For each available encoder × resolution tier × target device it spawns
/// a short synthetic source via the lavfi <c>testsrc</c> muxer and measures
/// the achieved fps from ffmpeg's structured progress output.
///
/// Hardware encoders are benchmarked once per matching GPU so a box with
/// multiple GPUs (two NVIDIA cards, iGPU + dGPU, etc.) gets a separate
/// entry per device. Software encoders run once; their device name is null.
/// Results persist via <see cref="ISpeedIndexStore"/> so subsequent server
/// starts reuse the measurements — see <see cref="NeedsRecalibration"/>.
/// </summary>
public class HardwareBenchmark(
    CodecRegistry codecRegistry,
    IHardwareCapabilities hardware,
    IProcessRunner processRunner,
    ISpeedIndexStore store,
    EncoderOptions options,
    ILogger<HardwareBenchmark> logger,
    IAnalysisProgressObserver? progress = null
) : IHardwareBenchmark
{
    // Standard benchmark tiers for every target.
    internal static readonly (int Width, int Height)[] DefaultTiers =
    [
        (1920, 1080),
        (1280, 720),
        (854, 480),
    ];

    // 4K tier — added to DefaultTiers for hardware encoders whose GPU has
    // enough VRAM to sustain 4K encoding (≥6 GB). Software encoders and
    // low-VRAM GPUs skip this tier: a slow software 4K calibration at
    // 30 frames still takes tens of seconds, and low-VRAM cards OOM or
    // fall back to a tiled path that's not representative of real encodes.
    private const int FourKCapableVramMb = 6_144;
    private static readonly (int Width, int Height) UhdTier = (3840, 2160);

    private const double CalibrationDurationSeconds = 1.0;
    private const double SourceFrameRate = 30.0;

    // Cap output to this many frames regardless of source length. Fast
    // encoders finish in milliseconds; slow ones (libaom-av1, librav1e)
    // can crawl at 0.3 fps — running a full 5-second source there eats
    // 8+ minutes per tier. 30 frames is enough to get a stable fps
    // reading while keeping the worst-case benchmark tractable.
    private const int MaxCalibrationFrames = 30;

    // Recalibrate once a month by default. Hardware / driver updates can
    // change real throughput noticeably.
    private static readonly TimeSpan RecalibrationInterval = TimeSpan.FromDays(30);

    private SpeedIndex _cache = new(new());

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

        IAnalysisProgressObserver observer = progress ?? NullAnalysisProgressObserver.Instance;
        string jobId = Guid.NewGuid().ToString("N");

        // Enumerate all (target, tier) pairs once upfront so we can compute
        // percent-complete as each probe finishes.
        List<(CalibrationTarget Target, int Width, int Height)> allWork = [];
        foreach (CalibrationTarget candidate in SelectCandidates())
        {
            foreach ((int w, int h) in TiersForTarget(candidate))
                allWork.Add((candidate, w, h));
        }

        int total = allWork.Count;
        int completed = 0;

        observer.Report(jobId, "benchmark", 0, "starting");

        foreach ((CalibrationTarget target, int tierWidth, int tierHeight) in allWork)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                SpeedMeasurement? measured = await MeasureAsync(target, tierWidth, tierHeight, ct);

                if (measured is null)
                    continue;

                SpeedKey key = new(
                    target.Codec,
                    target.Encoder.FfmpegName,
                    tierWidth,
                    target.Device?.Name
                );
                results[key] = measured;

                logger.LogInformation(
                    "Benchmarked {Encoder}{DeviceTag} @ {W}x{H}: {Fps:F1} fps ({Speed:F2}x)",
                    target.Encoder.FfmpegName,
                    target.Device is null ? " (CPU)" : $" on {target.Device.Name}",
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
                    "Benchmark failed for {Encoder}{DeviceTag} @ {W}x{H} — skipping",
                    target.Encoder.FfmpegName,
                    target.Device is null ? "" : $" on {target.Device.Name}",
                    tierWidth,
                    tierHeight
                );
            }
            finally
            {
                completed++;
                double pct = total > 0 ? (double)completed / total * 100.0 : 100.0;
                string stage = $"{target.Encoder.FfmpegName} {tierWidth}x{tierHeight}";
                observer.Report(jobId, "benchmark", pct, stage);
            }
        }

        observer.Report(jobId, "benchmark", 100, "done");

        SpeedIndex index = new(results);
        store.Save(index);
        _cache = index;
        return index;
    }

    /// <summary>
    /// Enumerates every (codec, encoder, device) target to benchmark.
    /// Software encoders yield once with <see cref="CalibrationTarget.Device"/>
    /// = null. Hardware encoders yield once per matching GPU so a host with
    /// multiple GPUs benchmarks each card independently — the assigner can
    /// later weight work across devices of different speeds.
    /// </summary>
    internal IEnumerable<CalibrationTarget> SelectCandidates()
    {
        foreach (
            (VideoCodecType codec, EncoderInfo encoder) in codecRegistry.EnumerateVideoEncoders()
        )
        {
            if (encoder.RequiredVendor is GpuVendor vendor)
            {
                List<(GpuDevice Device, int VendorIndex)> matchingGpus = EnumerateGpusForVendor(
                    vendor
                );
                if (matchingGpus.Count == 0)
                    continue; // Vendor not installed — skip encoder entirely.

                foreach ((GpuDevice device, int vendorIndex) in matchingGpus)
                    yield return new(codec, encoder, device, vendorIndex);
            }
            else
            {
                // Software encoder — no device, no index.
                yield return new(codec, encoder, Device: null, VendorIndex: 0);
            }
        }
    }

    /// <summary>
    /// Returns the tiers to benchmark for a given target. Hardware targets
    /// whose GPU has enough VRAM to sustain 4K encoding get the UHD tier
    /// prepended so the speed index captures their real 4K throughput.
    /// Low-VRAM GPUs and software targets skip 4K — software calibration
    /// at 4K is too slow to be practical even capped at 30 frames, and
    /// low-VRAM cards either OOM or fall back to a tiled encode path that
    /// doesn't represent real-world throughput.
    /// </summary>
    internal static IEnumerable<(int Width, int Height)> TiersForTarget(CalibrationTarget target)
    {
        if (target.Device is { VramMb: >= FourKCapableVramMb })
            yield return UhdTier;

        foreach ((int w, int h) in DefaultTiers)
            yield return (w, h);
    }

    /// <summary>
    /// Returns every GPU of the given vendor paired with its vendor-relative
    /// index (0 = first Nvidia, 1 = second Nvidia, …). The index is what
    /// ffmpeg expects in <c>cuda=cu:N</c> / <c>-gpu N</c> etc.
    /// </summary>
    private List<(GpuDevice Device, int VendorIndex)> EnumerateGpusForVendor(GpuVendor vendor)
    {
        List<(GpuDevice Device, int VendorIndex)> result = [];
        int idx = 0;
        foreach (GpuDevice gpu in hardware.Gpus)
        {
            if (gpu.Vendor != vendor)
                continue;

            result.Add((gpu, idx));
            idx++;
        }
        return result;
    }

    private async Task<SpeedMeasurement?> MeasureAsync(
        CalibrationTarget target,
        int width,
        int height,
        CancellationToken ct
    )
    {
        string[] arguments = BuildCalibrationArguments(target, width, height);

        ProgressParser parser = new();
        double observedFps = 0;
        int lastFrame = 0;

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = parser.FeedLine(line);
            if (snapshot is null)
                return;

            if (snapshot.Fps > 0)
                observedFps = Math.Max(observedFps, snapshot.Fps);
            if (snapshot.Frame > lastFrame)
                lastFrame = snapshot.Frame;
        }

        System.Diagnostics.Stopwatch wall = System.Diagnostics.Stopwatch.StartNew();
        ProcessResult result = await processRunner.RunAsync(
            options.FfmpegPath,
            arguments,
            OnStdOut,
            null,
            null,
            ct
        );
        wall.Stop();

        if (!result.IsSuccess)
        {
            // Log the ffmpeg stderr tail so users can see *why* an encoder
            // dropped out — commonly missing CUDA driver, no VA-API device,
            // or a vendor-specific flag incompatibility.
            logger.LogDebug(
                "Benchmark probe for {Encoder}{DeviceTag} @ {W}x{H} exited {Code}: {StdErr}",
                target.Encoder.FfmpegName,
                target.Device is null ? "" : $" on {target.Device.Name}",
                width,
                height,
                result.ExitCode,
                TruncateStderr(result.StdErr)
            );
            return null;
        }

        // ffmpeg's `fps=` progress field is computed over a one-second
        // sliding window — fast encoders like libx264 finish 30 frames in
        // under 100ms and never emit a non-zero fps line. Fall back to
        // wall-clock-derived fps so libx264 / nvenc / hwaccel encoders
        // actually land in the speed index instead of being silently
        // dropped. Only used when the progress stream gave us nothing
        // usable — measured fps wins when both are present because it's
        // closer to the encoder's steady-state rate (excludes process
        // spin-up time).
        if (observedFps <= 0 && lastFrame > 0 && wall.Elapsed.TotalSeconds > 0)
        {
            observedFps = lastFrame / wall.Elapsed.TotalSeconds;
            logger.LogDebug(
                "Wall-clock fps fallback for {Encoder} @ {W}x{H}: {Frames} frames in {Elapsed:F2}s = {Fps:F1} fps",
                target.Encoder.FfmpegName,
                width,
                height,
                lastFrame,
                wall.Elapsed.TotalSeconds,
                observedFps
            );
        }

        if (observedFps <= 0)
            return null;

        double multiplier = observedFps / SourceFrameRate;
        return new(Fps: observedFps, SpeedMultiplier: multiplier, MeasuredAt: DateTime.UtcNow);
    }

    internal static string[] BuildCalibrationArguments(
        CalibrationTarget target,
        int width,
        int height
    )
    {
        List<string> args = ["-hide_banner", "-nostats", "-loglevel", "error"];

        // Hardware device init — must come BEFORE the input flag so the
        // encoder's hw context is ready when frames arrive. Without this,
        // ffmpeg can fall back to CPU or fail the encoder entirely, making
        // our measurement meaningless.
        AddHwaccelInitArgs(args, target);

        args.Add("-f");
        args.Add("lavfi");
        args.Add("-i");
        args.Add(
            $"testsrc=duration={CalibrationDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}:size={width}x{height}:rate={SourceFrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        );

        // Upload lavfi frames to the GPU before the encoder consumes them.
        // Without the upload filter the encoder still works (modern ffmpeg
        // auto-uploads) but CPU→GPU transfer bleeds into the encode
        // measurement. Explicit hwupload keeps the fps number tied to GPU
        // encode throughput.
        AddHwUploadFilter(args, target);

        args.Add("-c:v");
        args.Add(target.Encoder.FfmpegName);

        // Vendor-specific flags (e.g. -usage transcoding for AMF).
        foreach ((string flag, string value) in target.Encoder.VendorSpecificFlags)
        {
            args.Add(flag);
            args.Add(value);
        }

        // GPU device selector on the encoder itself. Matters when the host
        // has multiple GPUs of the same vendor — without this, ffmpeg picks
        // the first one and every "per-device" benchmark actually exercises
        // the same card.
        AddEncoderDeviceSelector(args, target);

        // Use a reasonable default preset if the encoder has one. Matches
        // what production encodes would typically do.
        if (target.Encoder.Presets.Length > 0)
        {
            string preset =
                target.Encoder.Presets.Contains("medium") ? "medium"
                : target.Encoder.Presets.Contains("p4") ? "p4"
                : target.Encoder.Presets[target.Encoder.Presets.Length / 2];
            args.Add("-preset");
            args.Add(preset);
        }

        // Hard cap on encoded frames — ffmpeg stops as soon as the output
        // reaches this count, regardless of how much source remains. Keeps
        // slow encoders from holding the benchmark thread hostage.
        args.Add("-frames:v");
        args.Add(MaxCalibrationFrames.ToString(System.Globalization.CultureInfo.InvariantCulture));

        args.Add("-f");
        args.Add("null");
        args.Add("-");
        args.Add("-progress");
        args.Add("pipe:1");

        return [.. args];
    }

    /// <summary>
    /// Appends vendor-specific <c>-init_hw_device</c> arguments so the hw
    /// encoder wires itself to the real GPU (and the right GPU on
    /// multi-card systems). No-op for software encoders and for encoder
    /// families where ffmpeg's auto-init already handles device selection.
    /// </summary>
    private static void AddHwaccelInitArgs(List<string> args, CalibrationTarget target)
    {
        if (target.Encoder.RequiredVendor is not GpuVendor vendor)
            return;

        string deviceArg = target.VendorIndex.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );

        switch (vendor)
        {
            case GpuVendor.Nvidia:
                args.Add("-init_hw_device");
                args.Add($"cuda=cu:{deviceArg}");
                args.Add("-filter_hw_device");
                args.Add("cu");
                break;
            case GpuVendor.Intel:
                if (target.Encoder.FfmpegName.Contains("_qsv", StringComparison.OrdinalIgnoreCase))
                {
                    args.Add("-init_hw_device");
                    args.Add("qsv=hw");
                    args.Add("-filter_hw_device");
                    args.Add("hw");
                }
                break;
            case GpuVendor.Amd:
                // AMF on Windows runs on CPU-side frames natively; on Linux
                // it uses vaapi. We skip explicit init — the encoder
                // initializes on first use.
                break;
            case GpuVendor.Apple:
                // VideoToolbox auto-initializes on the default GPU.
                break;
        }
    }

    private static void AddHwUploadFilter(List<string> args, CalibrationTarget target)
    {
        if (target.Encoder.RequiredVendor is not GpuVendor vendor)
            return;

        string? filter = vendor switch
        {
            GpuVendor.Nvidia => "format=nv12,hwupload_cuda",
            GpuVendor.Intel
                when target.Encoder.FfmpegName.Contains(
                    "_qsv",
                    StringComparison.OrdinalIgnoreCase
                ) => "format=nv12,hwupload=extra_hw_frames=16",
            _ => null,
        };

        if (filter is null)
            return;

        args.Add("-vf");
        args.Add(filter);
    }

    /// <summary>
    /// Encoder-level device selector flag for encoders that accept one.
    /// NVENC uses <c>-gpu N</c>; other encoder families derive the device
    /// from <c>-init_hw_device</c> or leave selection to driver defaults.
    /// </summary>
    private static void AddEncoderDeviceSelector(List<string> args, CalibrationTarget target)
    {
        if (target.Device is null)
            return;

        if (
            target.Encoder.RequiredVendor == GpuVendor.Nvidia
            && target.Encoder.FfmpegName.Contains("_nvenc", StringComparison.OrdinalIgnoreCase)
        )
        {
            args.Add("-gpu");
            args.Add(
                target.VendorIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        }
    }

    private static string TruncateStderr(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return "<empty>";
        const int maxLen = 500;
        return stderr.Length > maxLen ? stderr[^maxLen..] : stderr;
    }
}

/// <summary>
/// One benchmark run target — an encoder + the specific GPU device it
/// targets (null for software encoders). <see cref="VendorIndex"/> is the
/// device's index among GPUs of the same vendor, used for ffmpeg's
/// per-vendor device selector args.
/// </summary>
public record CalibrationTarget(
    VideoCodecType Codec,
    EncoderInfo Encoder,
    GpuDevice? Device,
    int VendorIndex
);
