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
    private readonly ResourceBudgetOptions _options;
    private readonly ILogger<ResourceBudget>? _logger;
    private bool _headroomDenialLogged;

    // Live source of truth for GPUs. Held through IHardwareCapabilities so
    // we re-read it on every lookup — HardwareInitializationService finishes
    // detection AFTER the DI container instantiates this singleton, so
    // capturing GPUs at construction time gives an empty list forever (which
    // is what caused every encoder-gpu worker to crash on first job pick
    // with "GPU device 'h264_nvenc' is not registered" — alias registration
    // ran inside an empty foreach loop).
    private readonly IHardwareCapabilities? _hardware;
    private readonly Lock _registrationLock = new();
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
        ResourceBudgetOptions? options = null,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _hardware = hardware;
        _monitor = monitor;
        _options = options ?? ResourceBudgetOptions.Disabled;
        _logger = logger;
        _cpuSemaphore = new(initialCount: hardware.CpuCores, maxCount: hardware.CpuCores);
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
        ResourceBudgetOptions? options = null,
        ILogger<ResourceBudget>? logger = null
    )
    {
        _monitor = monitor;
        _options = options ?? ResourceBudgetOptions.Disabled;
        _logger = logger;
        _cpuSemaphore = new(initialCount: cpuCores, maxCount: cpuCores);
        _gpuSemaphores = new();
        RegisterGpus(gpuDevices: gpuDevices);
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

            RegisterGpus(gpuDevices: gpus);
            _gpusRegistered = true;

            _logger?.LogDebug(
                message: "ResourceBudget GPU semaphores registered lazily: {Count} device(s), "
                         + "{KeyCount} lookup keys (incl. vendor + encoder aliases)", args: [gpus.Count, _gpuSemaphores.Count]
            );
        }
    }

    private void RegisterGpus(IReadOnlyList<GpuDevice> gpuDevices)
    {
        foreach (GpuDevice device in gpuDevices)
        {
            SemaphoreSlim semaphore = new(initialCount: device.MaxEncoderSessions, maxCount: device.MaxEncoderSessions);
            _gpuSemaphores[key: device.Name] = semaphore;

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
                _gpuSemaphores.TryAdd(key: token, value: semaphore);

                // Also alias every concrete encoder FfmpegName that contains
                // the token — covers h264_nvenc / hevc_nvenc / av1_nvenc with
                // one loop iteration per vendor.
                foreach (string encoderName in EncoderNamesForVendor(token: token))
                    _gpuSemaphores.TryAdd(key: encoderName, value: semaphore);
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
            "nvenc" => GpuEncoderTokens.NvencNames,
            "amf" => GpuEncoderTokens.AmfNames,
            "qsv" => GpuEncoderTokens.QsvNames,
            "vaapi" => GpuEncoderTokens.VaapiNames,
            "videotoolbox" => GpuEncoderTokens.VideotoolboxNames,
            _ => [],
        };

    public int AvailableGpuEncoderSlots(string gpuDeviceKey)
    {
        return _gpuSemaphores.TryGetValue(key: gpuDeviceKey, value: out SemaphoreSlim? semaphore)
            ? semaphore.CurrentCount
            : 0;
    }

    public double CurrentGpuEncodeUtilization(string gpuDeviceKey) =>
        _monitor?.GetGpuEncodeUtilization(gpuDeviceKey: gpuDeviceKey) ?? 0.0;

    public int AvailableCpuThreads()
    {
        return _cpuSemaphore.CurrentCount;
    }

    // Unlike AcquireAsync (cancellable) and TryAcquire (caller-supplied
    // timeout), the interface's Acquire has no way to pass a timeout at all.
    // Bounding the default blocking wait means a saturated budget can no
    // longer hang a synchronous caller's thread forever, and gives the
    // rollback-on-timeout path below something to trigger on.
    private static readonly TimeSpan DefaultAcquireTimeout = TimeSpan.FromSeconds(seconds: 30);

    public ResourceLease Acquire(ResourceRequirement requirement) =>
        Acquire(requirement: requirement, timeout: DefaultAcquireTimeout);

    /// <summary>
    /// Same contract as <see cref="Acquire(ResourceRequirement)"/> with an
    /// explicit timeout. Rolls back any slots already granted (e.g. GPU slots
    /// acquired, then the CPU wait times out) instead of leaking them — a
    /// caller that never receives a <see cref="ResourceLease"/> has no way to
    /// release what a partial acquisition already took.
    /// </summary>
    public ResourceLease Acquire(ResourceRequirement requirement, TimeSpan timeout)
    {
        int acquiredGpuSlots = 0;
        SemaphoreSlim? gpuSemaphore = null;
        int acquiredCpuThreads = 0;

        try
        {
            if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
            {
                gpuSemaphore = GetGpuSemaphore(gpuDeviceKey: requirement.GpuDeviceKey);

                for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
                {
                    if (!gpuSemaphore.Wait(timeout: timeout))
                    {
                        throw new TimeoutException(
                            message: $"Timed out acquiring GPU slot {slotIndex + 1}/{requirement.GpuSlots} "
                                     + $"on {requirement.GpuDeviceKey} after {timeout}."
                        );
                    }

                    acquiredGpuSlots++;
                }

                _logger?.LogDebug(
                    message: "Acquired {GpuSlots} GPU slot(s) on {GpuKey}", args: [requirement.GpuSlots, requirement.GpuDeviceKey]
                );
            }

            if (requirement.CpuThreads > 0)
            {
                for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
                {
                    if (!_cpuSemaphore.Wait(timeout: timeout))
                    {
                        throw new TimeoutException(
                            message: $"Timed out acquiring CPU thread {threadIndex + 1}/{requirement.CpuThreads} "
                                     + $"after {timeout}."
                        );
                    }

                    acquiredCpuThreads++;
                }

                _logger?.LogDebug(message: "Acquired {CpuThreads} CPU thread(s)", args: requirement.CpuThreads);
            }
        }
        catch
        {
            if (gpuSemaphore is not null && acquiredGpuSlots > 0)
                gpuSemaphore.Release(releaseCount: acquiredGpuSlots);
            if (acquiredCpuThreads > 0)
                _cpuSemaphore.Release(releaseCount: acquiredCpuThreads);
            throw;
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug(message: "Lease {LeaseId} granted", args: leaseId);

        return new(LeaseId: leaseId, GpuDeviceKey: requirement.GpuDeviceKey, GpuSlots: requirement.GpuSlots, CpuThreads: requirement.CpuThreads);
    }

    public async Task<ResourceLease> AcquireAsync(
        ResourceRequirement requirement,
        CancellationToken cancellationToken = default
    )
    {
        int acquiredGpuSlots = 0;
        SemaphoreSlim? gpuSemaphore = null;
        int acquiredCpuThreads = 0;

        try
        {
            if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
            {
                gpuSemaphore = GetGpuSemaphore(gpuDeviceKey: requirement.GpuDeviceKey);

                for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
                {
                    await gpuSemaphore.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    acquiredGpuSlots++;
                }

                _logger?.LogDebug(
                    message: "Acquired {GpuSlots} GPU slot(s) on {GpuKey}", args: [requirement.GpuSlots, requirement.GpuDeviceKey]
                );
            }

            if (requirement.CpuThreads > 0)
            {
                for (int threadIndex = 0; threadIndex < requirement.CpuThreads; threadIndex++)
                {
                    await _cpuSemaphore.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    acquiredCpuThreads++;
                }

                _logger?.LogDebug(message: "Acquired {CpuThreads} CPU thread(s)", args: requirement.CpuThreads);
            }
        }
        catch (OperationCanceledException)
        {
            // Roll back partial acquisitions so cancelled callers don't leak slots.
            if (gpuSemaphore is not null && acquiredGpuSlots > 0)
                gpuSemaphore.Release(releaseCount: acquiredGpuSlots);
            if (acquiredCpuThreads > 0)
                _cpuSemaphore.Release(releaseCount: acquiredCpuThreads);
            throw;
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug(message: "Lease {LeaseId} granted via AcquireAsync", args: leaseId);

        return new(LeaseId: leaseId, GpuDeviceKey: requirement.GpuDeviceKey, GpuSlots: requirement.GpuSlots, CpuThreads: requirement.CpuThreads);
    }

    public ResourceLease? TryAcquire(ResourceRequirement requirement, TimeSpan timeout)
    {
        int timeoutMs = (int)timeout.TotalMilliseconds;

        // Live-headroom gate. Static semaphores cap peak concurrency at
        // hardware limits, but the count we can actually sustain depends on
        // current load. Refuse the lease when the host is saturated; the
        // worker retries every BudgetRetryDelay so we re-check as load drops.
        if (!HasHeadroom(requirement: requirement, logIfDenied: timeoutMs > 0))
            return null;

        SemaphoreSlim? gpuSemaphore = null;
        if (requirement.GpuDeviceKey is not null && requirement.GpuSlots > 0)
        {
            try
            {
                gpuSemaphore = GetGpuSemaphore(gpuDeviceKey: requirement.GpuDeviceKey);
            }
            catch (InvalidOperationException)
            {
                // GPU key not registered (yet).
                return null;
            }

            int acquiredGpuSlots = 0;

            for (int slotIndex = 0; slotIndex < requirement.GpuSlots; slotIndex++)
            {
                if (!gpuSemaphore.Wait(millisecondsTimeout: timeoutMs))
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
                            message: "TryAcquire timed out acquiring GPU slot {Slot}/{Total} on {GpuKey}", args: [slotIndex + 1, requirement.GpuSlots, requirement.GpuDeviceKey]
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
                if (!_cpuSemaphore.Wait(millisecondsTimeout: timeoutMs))
                {
                    for (int rollback = 0; rollback < acquiredCpuThreads; rollback++)
                    {
                        _cpuSemaphore.Release();
                    }

                    if (gpuSemaphore is not null && requirement.GpuSlots > 0)
                    {
                        for (int rollback = 0; rollback < requirement.GpuSlots; rollback++)
                        {
                            gpuSemaphore.Release();
                        }
                    }

                    if (timeoutMs > 0)
                    {
                        _logger?.LogDebug(
                            message: "TryAcquire timed out acquiring CPU thread {Thread}/{Total}, rolled back GPU slots", args: [threadIndex + 1, requirement.CpuThreads]
                        );
                    }

                    return null;
                }

                acquiredCpuThreads++;
            }
        }

        string leaseId = Ulid.NewUlid().ToString();

        _logger?.LogDebug(message: "Lease {LeaseId} granted via TryAcquire", args: leaseId);

        return new(LeaseId: leaseId, GpuDeviceKey: requirement.GpuDeviceKey, GpuSlots: requirement.GpuSlots, CpuThreads: requirement.CpuThreads);
    }

    private bool HasHeadroom(ResourceRequirement requirement, bool logIfDenied)
    {
        if (_monitor is null)
            return true;

        if (_options.CpuHeadroomPercent > 0)
        {
            double systemCpu = _monitor.GetSystemCpuUsagePercent();
            if (systemCpu >= _options.CpuHeadroomPercent)
            {
                LogHeadroomDenied(
                    signal: "system CPU",
                    current: systemCpu,
                    threshold: _options.CpuHeadroomPercent,
                    emit: logIfDenied
                );
                return false;
            }
        }

        if (
            _options.GpuHeadroomPercent > 0
            && requirement.GpuDeviceKey is not null
            && requirement.GpuSlots > 0
        )
        {
            double gpuUtil = _monitor.GetGpuEncodeUtilization(gpuDeviceKey: requirement.GpuDeviceKey) * 100.0;
            if (gpuUtil >= _options.GpuHeadroomPercent)
            {
                LogHeadroomDenied(
                    signal: $"GPU encode '{requirement.GpuDeviceKey}'",
                    current: gpuUtil,
                    threshold: _options.GpuHeadroomPercent,
                    emit: logIfDenied
                );
                return false;
            }
        }

        if (_options.MinFreeMemoryMb > 0)
        {
            long freeMb = _monitor.GetAvailableMemoryMb();
            if (freeMb > 0 && freeMb < _options.MinFreeMemoryMb)
            {
                LogHeadroomDenied(
                    signal: "free memory MB",
                    current: freeMb,
                    threshold: _options.MinFreeMemoryMb,
                    emit: logIfDenied,
                    invert: true
                );
                return false;
            }
        }

        _headroomDenialLogged = false;
        return true;
    }

    private void LogHeadroomDenied(
        string signal,
        double current,
        double threshold,
        bool emit,
        bool invert = false
    )
    {
        if (!emit || _headroomDenialLogged)
            return;

        _headroomDenialLogged = true;
        string comparison = invert ? "below" : "above";
        _logger?.LogDebug(
            message: "Headroom gate denied lease — {Signal} at {Current:F1} is {Cmp} threshold {Threshold:F1}", args: [signal, current, comparison, threshold]
        );
    }

    public void Release(ResourceLease lease)
    {
        if (lease.GpuDeviceKey is not null && lease.GpuSlots > 0)
        {
            SemaphoreSlim gpuSemaphore = GetGpuSemaphore(gpuDeviceKey: lease.GpuDeviceKey);
            gpuSemaphore.Release(releaseCount: lease.GpuSlots);

            _logger?.LogDebug(
                message: "Released {GpuSlots} GPU slot(s) on {GpuKey} for lease {LeaseId}", args: [lease.GpuSlots, lease.GpuDeviceKey, lease.LeaseId]
            );
        }

        if (lease.CpuThreads > 0)
        {
            _cpuSemaphore.Release(releaseCount: lease.CpuThreads);

            _logger?.LogDebug(
                message: "Released {CpuThreads} CPU thread(s) for lease {LeaseId}", args: [lease.CpuThreads, lease.LeaseId]
            );
        }
    }

    public bool IsGpuDeviceRegistered(string gpuDeviceKey)
    {
        if (_gpuSemaphores.ContainsKey(key: gpuDeviceKey))
            return true;

        // Detection may have completed since construction (see the _hardware
        // field comment above) — retry registration once before concluding
        // the key is genuinely absent rather than just not yet registered.
        TryRegisterGpus();
        return _gpuSemaphores.ContainsKey(key: gpuDeviceKey);
    }

    private SemaphoreSlim GetGpuSemaphore(string gpuDeviceKey)
    {
        if (_gpuSemaphores.TryGetValue(key: gpuDeviceKey, value: out SemaphoreSlim? semaphore))
            return semaphore;

        // Detection may have completed since the DI container instantiated us
        // with an empty GPU list. Try registering now and re-check.
        TryRegisterGpus();
        if (_gpuSemaphores.TryGetValue(key: gpuDeviceKey, value: out semaphore))
            return semaphore;

        throw new InvalidOperationException(
            message: $"GPU device '{gpuDeviceKey}' is not registered with this ResourceBudget. "
                     + $"Available keys: {string.Join(separator: ", ", values: _gpuSemaphores.Keys)}"
        );
    }
}
