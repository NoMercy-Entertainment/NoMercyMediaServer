using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Resources;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Production implementation of <see cref="IResourceBudget"/>.
/// GPU devices are keyed by their canonical name string so this class can be
/// constructed from either <see cref="GpuDevice"/> lists or plain strings.
/// </summary>
public class ResourceBudget : IResourceBudget
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gpuSemaphores;
    private readonly SemaphoreSlim _cpuSemaphore;
    private readonly IResourceMonitor? _monitor;
    private readonly ILogger<ResourceBudget>? _logger;

    // Live source of truth for GPUs. Held through IHardwareCapabilities so
    // we re-read it on every lookup — HardwareInitializationService finishes
    // detection AFTER the DI container instantiates this singleton, so
    // capturing GPUs at construction time gives an empty list forever (which
    // is what caused every encoder-gpu worker to crash on first job pick
    // with "GPU device 'h264_nvenc' is not registered" — alias registration
    // ran inside an empty foreach loop).
    private readonly IHardwareCapabilities? _hardware;
    private readonly object _registrationLock = new();
    private bool _gpusRegistered;

    // FFmpeg encoder name suffixes that identify a GPU vendor. Same list as
    // TaskResourceHelper.GpuEncoderTokens — kept in sync so encoder-name
    // resource requirements (e.g. "h264_nvenc") resolve to the right GPU
    // semaphore. Vendor tokens are registered as alias keys alongside each
    // GpuDevice.Name so callers can hand us either form.
    private static readonly (string Token, GpuVendor Vendor)[] VendorTokens =
    [
        ("nvenc", GpuVendor.Nvidia),
        ("cuvid", GpuVendor.Nvidia),
        ("amf", GpuVendor.Amd),
        ("qsv", GpuVendor.Intel),
        ("vaapi", GpuVendor.Intel),
        ("videotoolbox", GpuVendor.Apple),
    ];

    /// <summary>
    /// Primary constructor used by DI. Holds <see cref="IHardwareCapabilities"/>
    /// live so the GPU list is re-read after async detection completes — see
    /// <c>_hardware</c> field comment for why eager capture is broken.
    /// </summary>
    public ResourceBudget(
        IHardwareCapabilities hardware,
        IResourceMonitor? monitor = null,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _hardware = hardware;
        _monitor = monitor;
        _logger = logger;
        _cpuSemaphore = new(hardware.CpuCores, hardware.CpuCores);
        _gpuSemaphores = new();
        TryRegisterGpus();
    }

    /// <summary>
    /// Legacy constructor used by tests that pass an explicit GPU list (and
    /// don't want to spin up an IHardwareCapabilities). Marks the GPU set as
    /// final so lazy registration doesn't fire and shadow the test fixture.
    /// </summary>
    public ResourceBudget(
        IReadOnlyList<GpuDevice> gpuDevices,
        int cpuCores,
        IResourceMonitor? monitor = null,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _monitor = monitor;
        _logger = logger;
        _cpuSemaphore = new(cpuCores, cpuCores);
        _gpuSemaphores = new();
        RegisterGpus(gpuDevices);
        _gpusRegistered = true; // explicit list — no lazy re-registration
    }

    private void TryRegisterGpus()
    {
        if (_gpusRegistered || _hardware is null)
            return;

        lock (_registrationLock)
        {
            if (_gpusRegistered)
                return;

            IReadOnlyList<GpuDevice> gpus = _hardware.Gpus;
            if (gpus.Count == 0)
                return; // detection still pending — try again on next lookup

            RegisterGpus(gpus);
            _gpusRegistered = true;

            _logger?.LogDebug(
                "ResourceBudget GPU semaphores registered lazily: {Count} device(s), "
                    + "{KeyCount} lookup keys (incl. vendor + encoder aliases)",
                gpus.Count,
                _gpuSemaphores.Count
            );
        }
    }

    private void RegisterGpus(IReadOnlyList<GpuDevice> gpuDevices)
    {
        foreach (GpuDevice device in gpuDevices)
        {
            SemaphoreSlim semaphore = new(device.MaxEncoderSessions, device.MaxEncoderSessions);
            _gpuSemaphores[device.Name] = semaphore;

            // Alias the same semaphore under every encoder vendor token this
            // GPU supports, so a ResourceRequirement keyed by encoder name
            // ("h264_nvenc", "hevc_nvenc", …) resolves to the same per-device
            // slot pool as one keyed by GPU name. Multi-GPU same-vendor systems
            // share a single semaphore per vendor — acceptable trade-off
            // against the alternative of every encoder-gpu worker crashing on
            // "device 'h264_nvenc' is not registered."
            foreach ((string token, GpuVendor vendor) in VendorTokens)
            {
                if (vendor != device.Vendor)
                    continue;

                // First-vendor-wins: don't clobber another GPU's alias when
                // two same-vendor GPUs are present. The first becomes the
                // default lane for that vendor; second-GPU callers must opt in
                // by passing GpuDevice.Name explicitly.
                _gpuSemaphores.TryAdd(token, semaphore);

                // Also alias every concrete encoder FfmpegName that contains
                // the token — covers h264_nvenc / hevc_nvenc / av1_nvenc with
                // one loop iteration per vendor.
                foreach (string encoderName in EncoderNamesForVendor(token))
                    _gpuSemaphores.TryAdd(encoderName, semaphore);
            }
        }
    }

    /// <summary>
    /// Encoder FfmpegName values that should resolve to a vendor's GPU
    /// semaphore. Kept in sync with the per-codec definitions in
    /// <c>NoMercy.Encoder.Codecs.Definitions</c>.
    /// </summary>
    private static IEnumerable<string> EncoderNamesForVendor(string token) =>
        token switch
        {
            "nvenc" => ["h264_nvenc", "hevc_nvenc", "av1_nvenc"],
            "amf" => ["h264_amf", "hevc_amf", "av1_amf"],
            "qsv" => ["h264_qsv", "hevc_qsv", "av1_qsv", "vp9_qsv"],
            "vaapi" => ["h264_vaapi", "hevc_vaapi", "av1_vaapi", "vp9_vaapi"],
            "videotoolbox" => ["h264_videotoolbox", "hevc_videotoolbox"],
            _ => [],
        };

    public int AvailableGpuEncoderSlots(string gpuDeviceKey)
    {
        return _gpuSemaphores.TryGetValue(gpuDeviceKey, out SemaphoreSlim? semaphore)
            ? semaphore.CurrentCount
            : 0;
    }

    public double CurrentGpuEncodeUtilization(string gpuDeviceKey) =>
        _monitor?.GetGpuEncodeUtilization(gpuDeviceKey) ?? 0.0;

    public int AvailableCpuThreads()
    {
        return _cpuSemaphore.CurrentCount;
    }

    public ResourceLease Acquire(ResourceRequirement requirement)
    {
        if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);

            for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
            {
                gpuSemaphore.Wait();
            }

            _logger?.LogDebug(
                "Acquired {GpuSlots} GPU slot(s) on {GpuKey}",
                requirement.GpuSlots,
                requirement.GpuDeviceKey
            );
        }

        if (requirement.CpuThreads > 0)
        {
            for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
            {
                _cpuSemaphore.Wait();
            }

            _logger?.LogDebug("Acquired {CpuThreads} CPU thread(s)", requirement.CpuThreads);
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted", leaseId);

        return new(leaseId, requirement.GpuDeviceKey, requirement.GpuSlots, requirement.CpuThreads);
    }

    public ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout)
    {
        int timeoutMs = (int)timeout.TotalMilliseconds;

        if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);
            int acquiredGpuSlots = 0;

            for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
            {
                if (!gpuSemaphore.Wait(timeoutMs))
                {
                    for (int rollback = 0; rollback < acquiredGpuSlots; rollback++)
                    {
                        gpuSemaphore.Release();
                    }

                    // Only log when an actual wait elapsed — the immediate
                    // polling path (timeoutMs == 0) from QueueWorker is the
                    // expected "budget saturated, try later" loop. Worker
                    // logs the saturation episode once on its own; this
                    // line would fire per-rung × per-worker × per-retry and
                    // bury the rest of the encoder log under thousands of
                    // identical messages.
                    if (timeoutMs > 0)
                    {
                        _logger?.LogDebug(
                            "TryAcquire timed out acquiring GPU slot {Slot}/{Total} on {GpuKey}",
                            slotIndex + 1,
                            requirement.GpuSlots,
                            requirement.GpuDeviceKey
                        );
                    }

                    return null;
                }

                acquiredGpuSlots++;
            }
        }

        if (requirement.CpuThreads > 0)
        {
            int acquiredCpuThreads = 0;

            for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
            {
                if (!_cpuSemaphore.Wait(timeoutMs))
                {
                    for (int rollback = 0; rollback < acquiredCpuThreads; rollback++)
                    {
                        _cpuSemaphore.Release();
                    }

                    if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
                    {
                        SemaphoreSlim gpuSemaphore = GetGpuSemaphore(requirement.GpuDeviceKey);

                        for (int rollback = 0; rollback < requirement.GpuSlots; rollback++)
                        {
                            gpuSemaphore.Release();
                        }
                    }

                    if (timeoutMs > 0)
                    {
                        _logger?.LogDebug(
                            "TryAcquire timed out acquiring CPU thread {Thread}/{Total}, rolled back GPU slots",
                            threadIndex + 1,
                            requirement.CpuThreads
                        );
                    }

                    return null;
                }

                acquiredCpuThreads++;
            }
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug("Lease {LeaseId} granted via TryAcquire", leaseId);

        return new(leaseId, requirement.GpuDeviceKey, requirement.GpuSlots, requirement.CpuThreads);
    }

    public void Release(ResourceLease lease)
    {
        if (lease.GpuDeviceKey is not null && lease.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(lease.GpuDeviceKey);
            gpuSemaphore.Release(lease.GpuSlots);

            _logger?.LogDebug(
                "Released {GpuSlots} GPU slot(s) on {GpuKey} for lease {LeaseId}",
                lease.GpuSlots,
                lease.GpuDeviceKey,
                lease.LeaseId
            );
        }

        if (lease.CpuThreads > 0)
        {
            _cpuSemaphore.Release(lease.CpuThreads);

            _logger?.LogDebug(
                "Released {CpuThreads} CPU thread(s) for lease {LeaseId}",
                lease.CpuThreads,
                lease.LeaseId
            );
        }
    }

    private SemaphoreSlim GetGpuSemaphore(string gpuDeviceKey)
    {
        if (_gpuSemaphores.TryGetValue(gpuDeviceKey, out SemaphoreSlim? semaphore))
            return semaphore;

        // Detection may have completed since the DI container instantiated us
        // with an empty GPU list. Try registering now and re-check.
        TryRegisterGpus();
        if (_gpuSemaphores.TryGetValue(gpuDeviceKey, out semaphore))
            return semaphore;

        throw new InvalidOperationException(
            $"GPU device '{gpuDeviceKey}' is not registered with this ResourceBudget. "
                + $"Available keys: {string.Join(", ", _gpuSemaphores.Keys)}"
        );
    }
}
