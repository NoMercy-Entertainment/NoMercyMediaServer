namespace NoMercy.Encoder.Distribution;

using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;

/// <summary>
/// Background service that self-registers this process as a remote worker
/// with a coordinator, keeps the registration fresh via periodic
/// heartbeats, and unregisters cleanly on shutdown.
///
/// Only runs when <see cref="EncoderOptions.CoordinatorUrl"/> is set.
/// Standalone servers and coordinators themselves skip this — a
/// coordinator registering itself as its own worker works but isn't the
/// default; the operator has to explicitly opt in by pointing
/// CoordinatorUrl at the same host.
///
/// Network failures don't crash the process; the service logs warnings
/// and retries on the next heartbeat interval. The coordinator's
/// registry evicts stale workers after 60s of silence, so a brief
/// outage self-heals when the worker comes back.
/// </summary>
public class WorkerSelfRegistrationService(
    IHardwareCapabilities capabilities,
    EncoderOptions options,
    IHttpClientFactory httpClientFactory,
    ILogger<WorkerSelfRegistrationService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ShouldRun())
        {
            logger.LogInformation(
                "Worker self-registration disabled (CoordinatorUrl and WorkerSelfBaseUrl both required)"
            );
            return;
        }

        HttpClient http = httpClientFactory.CreateClient("worker-self-registration");
        http.BaseAddress = new Uri(options.CoordinatorUrl!);
        http.Timeout = TimeSpan.FromSeconds(10);

        if (!await TryRegisterAsync(http, stoppingToken).ConfigureAwait(false))
        {
            logger.LogWarning(
                "Initial registration with {CoordinatorUrl} failed — will retry on heartbeat loop",
                options.CoordinatorUrl
            );
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.WorkerHeartbeatInterval, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            bool alive = await TryHeartbeatAsync(http, stoppingToken).ConfigureAwait(false);
            if (!alive)
            {
                // Heartbeat bounced — coordinator doesn't know us. Could be
                // coordinator restart or our eviction after a long outage.
                // Re-register rather than spamming a 404 heartbeat loop.
                logger.LogInformation(
                    "Heartbeat rejected by coordinator — attempting re-registration"
                );
                await TryRegisterAsync(http, stoppingToken).ConfigureAwait(false);
            }
        }

        await TryUnregisterAsync(http).ConfigureAwait(false);
    }

    private bool ShouldRun() =>
        !string.IsNullOrWhiteSpace(options.CoordinatorUrl)
        && !string.IsNullOrWhiteSpace(options.WorkerSelfBaseUrl)
        && options.IsDistributedEncodingEnabled;

    private async Task<bool> TryRegisterAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                worker_id = options.WorkerId,
                base_url = options.WorkerSelfBaseUrl,
                cpu_cores = capabilities.CpuCores,
                available_cpu_threads = capabilities.CpuCores,
                available_gpu_slots = capabilities.Gpus.Sum(g => g.MaxEncoderSessions),
                gpus = capabilities.Gpus,
            };

            HttpResponseMessage response = await http.PostAsJsonAsync(
                    "api/v1/dashboard/workers/register",
                    payload,
                    ct
                )
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Registered as worker {WorkerId} at {CoordinatorUrl}",
                    options.WorkerId,
                    options.CoordinatorUrl
                );
                return true;
            }

            logger.LogWarning(
                "Coordinator returned {StatusCode} on register — distribution disabled on coordinator side?",
                (int)response.StatusCode
            );
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Self-registration request failed");
            return false;
        }
    }

    private async Task<bool> TryHeartbeatAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                available_cpu_threads = capabilities.CpuCores,
                available_gpu_slots = capabilities.Gpus.Sum(g => g.MaxEncoderSessions),
                gpu_utilization = 0.0,
            };

            HttpResponseMessage response = await http.PostAsJsonAsync(
                    $"api/v1/dashboard/workers/{options.WorkerId}/heartbeat",
                    payload,
                    ct
                )
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Heartbeat failed");
            return false;
        }
    }

    private async Task TryUnregisterAsync(HttpClient http)
    {
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            await http.DeleteAsync($"api/v1/dashboard/workers/{options.WorkerId}", cts.Token)
                .ConfigureAwait(false);
            logger.LogInformation("Unregistered worker {WorkerId} on shutdown", options.WorkerId);
        }
        catch (Exception ex)
        {
            // Shutdown path — log and move on. Coordinator's stale eviction
            // will clean us up within 60s if we can't confirm.
            logger.LogDebug(ex, "Unregister on shutdown failed (coordinator will evict stale)");
        }
    }
}
