namespace NoMercy.Encoder.Startup;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

public class HardwareInitializationService(
    IHardwareDetector hardwareDetector,
    FfmpegCapabilities ffmpegCapabilities,
    IDriverChangeDetector driverChangeDetector,
    IBenchmarkJobTracker benchmarkJobTracker,
    HardwareCapabilitiesHolder capabilitiesHolder,
    ILogger<HardwareInitializationService> logger
) : IHostedService
{
    public bool IsReady { get; private set; }

    public IHardwareCapabilities? Capabilities
    {
        get => capabilitiesHolder.Current;
        private set => capabilitiesHolder.Current = value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting hardware detection...");

        try
        {
            // Probe FFmpeg capabilities FIRST — GPU detection needs HasEncoder() to be populated
            await ffmpegCapabilities.ProbeAsync(cancellationToken);
            logger.LogInformation(
                "FFmpeg: {EncoderCount} encoders, {FilterCount} filters",
                ffmpegCapabilities.AvailableEncoders.Count,
                ffmpegCapabilities.AvailableFilters.Count
            );

            IReadOnlyList<GpuDevice> gpus = await hardwareDetector.DetectGpusAsync(
                cancellationToken
            );
            int cpuCores = await hardwareDetector.DetectCpuCoreCountAsync(cancellationToken);

            logger.LogInformation(
                "Detected {GpuCount} GPU(s), {CpuCores} CPU cores",
                gpus.Count,
                cpuCores
            );

            foreach (GpuDevice gpu in gpus)
                logger.LogInformation(
                    "GPU: {Vendor} {Name} ({VramMb}MB VRAM, max {Sessions} sessions)",
                    gpu.Vendor,
                    gpu.Name,
                    gpu.VramMb,
                    gpu.MaxEncoderSessions
                );

            Capabilities = new HardwareCapabilities(gpus, cpuCores);
            IsReady = true;
            logger.LogInformation("Hardware detection complete. Encoder ready.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Hardware detection failed — software-only fallback");
            Capabilities = new HardwareCapabilities(Gpus: [], CpuCores: Environment.ProcessorCount);
            IsReady = true;
        }

        // Driver change detection — runs in its own try-catch so a failure
        // here (fingerprint store unavailable, mock-of in tests, etc.) cannot
        // revert the Capabilities we just established. First boot:
        // HardwareBenchmarkBackgroundService handles initial calibration;
        // subsequent boots with a changed driver queue an immediate recalibration.
        try
        {
            DriverChangeResult driverResult = await driverChangeDetector.DetectAndPersistAsync(
                cancellationToken
            );

            if (driverResult.IsFirstBoot)
            {
                logger.LogInformation(
                    "Driver fingerprint: first boot (hash {Hash}) — initial calibration deferred to benchmark service",
                    driverResult.CurrentHash
                );
            }
            else if (driverResult.Changed)
            {
                logger.LogWarning(
                    "GPU driver change detected (prev={Prev}, curr={Curr}) — queuing benchmark recalibration",
                    driverResult.PreviousHash,
                    driverResult.CurrentHash
                );
                benchmarkJobTracker.Start(Array.Empty<VideoCodecType>(), Array.Empty<int>());
            }
            else
            {
                logger.LogInformation(
                    "Driver fingerprint unchanged (hash {Hash})",
                    driverResult.CurrentHash
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Driver fingerprint check failed — continuing without recalibration trigger"
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
