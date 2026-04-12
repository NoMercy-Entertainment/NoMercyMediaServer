namespace NoMercy.Encoder.V3.Startup;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Hardware;

public class HardwareInitializationService(
    IHardwareDetector hardwareDetector,
    FfmpegCapabilities ffmpegCapabilities,
    ILogger<HardwareInitializationService> logger
) : IHostedService
{
    public bool IsReady { get; private set; }
    public IHardwareCapabilities? Capabilities { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting hardware detection...");

        try
        {
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

            await ffmpegCapabilities.ProbeAsync(cancellationToken);
            logger.LogInformation(
                "FFmpeg: {EncoderCount} encoders, {FilterCount} filters",
                ffmpegCapabilities.AvailableEncoders.Count,
                ffmpegCapabilities.AvailableFilters.Count
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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
