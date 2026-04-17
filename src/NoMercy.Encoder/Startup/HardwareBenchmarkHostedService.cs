namespace NoMercy.Encoder.Startup;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;

/// <summary>
/// Runs an asynchronous hardware benchmark on server startup when the cache
/// is empty or stale. Gated by <see cref="EncoderOptions.AutoCalibrate"/>
/// so users running on low-spec boxes can opt out. Benchmark runs on a
/// background task — it never blocks the main host pipeline.
/// </summary>
public class HardwareBenchmarkHostedService(
    IHardwareBenchmark benchmark,
    EncoderOptions options,
    ILogger<HardwareBenchmarkHostedService> logger
) : IHostedService
{
    private Task? _benchmarkTask;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.AutoCalibrate)
        {
            logger.LogDebug("AutoCalibrate disabled — skipping benchmark");
            return Task.CompletedTask;
        }

        if (!benchmark.NeedsRecalibration())
        {
            logger.LogDebug("SpeedIndex cache is fresh — skipping benchmark");
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _benchmarkTask = Task.Run(() => RunBenchmarkAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        if (_benchmarkTask is not null)
        {
            try
            {
                await _benchmarkTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        _cts?.Dispose();
    }

    private async Task RunBenchmarkAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Starting hardware benchmark calibration");
            SpeedIndex result = await benchmark.CalibrateAsync(ct).ConfigureAwait(false);
            logger.LogInformation(
                "Hardware benchmark finished — captured {Count} measurements",
                result.Measurements.Count
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Hardware benchmark cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hardware benchmark failed");
        }
    }
}
