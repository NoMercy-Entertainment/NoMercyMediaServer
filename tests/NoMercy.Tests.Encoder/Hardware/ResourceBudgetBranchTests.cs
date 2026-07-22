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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Deepens <see cref="ResourceBudgetTests"/> with the branch-coverage gaps
/// the existing tests don't reach. ResourceBudget is the encoder's only
/// resource gatekeeper — wrong rollback on cancellation, wrong vendor-token
/// alias, or wrong "key not registered" fallback all manifest as either
/// leaked GPU slots (worker hangs forever) or "device not registered"
/// crashes on first job pick.
///
/// Each category below pins one specific failure mode:
///
/// • Vendor-token aliases — h264_nvenc / hevc_nvenc / av1_nvenc all resolve
///   to the SAME semaphore as the GPU's canonical Name. Acquiring through
///   one key must decrease availability when queried through any other.
/// • Multi-GPU same-vendor — first GPU wins the vendor token; second
///   GPU is reachable only via its explicit Name.
/// • Cancellation rollback — AcquireAsync partial-acquire on cancellation
///   must release every slot it grabbed before throwing.
/// • TryAcquire CPU-timeout rollback — when CPU acquisition times out
///   after GPU slots were taken, GPU slots must be released too.
/// • Unknown key — AvailableGpuEncoderSlots returns 0 (not throws);
///   GetGpuSemaphore (via Acquire) throws InvalidOperationException with
///   the list of available keys.
/// • Monitor wiring — CurrentGpuEncodeUtilization returns the monitor's
///   value when set, 0.0 when null.
/// • IHardwareCapabilities constructor — uses live read so lazy registration
///   fires when the capabilities list populates after construction.
/// </summary>
public class ResourceBudgetBranchTests
{
    private static GpuDevice Nvidia(string name = "RTX 4090", int sessions = 3) =>
        new(
            Vendor: GpuVendor.Nvidia,
            Name: name,
            VramMb: 24576,
            MaxEncoderSessions: sessions,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );

    private static GpuDevice Amd(string name = "RX 7900 XT", int sessions = 2) =>
        new(
            Vendor: GpuVendor.Amd,
            Name: name,
            VramMb: 20480,
            MaxEncoderSessions: sessions,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );

    // ── Vendor-token aliases ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "nvenc")]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    public void Nvidia_vendor_token_aliases_share_semaphore_with_canonical_name(string alias)
    {
        // Acquiring through alias must decrease the canonical-name count.
        GpuDevice gpu = Nvidia(sessions: 3);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);

        ResourceLease lease = budget.Acquire(requirement: new(GpuDeviceKey: alias, GpuSlots: 1, CpuThreads: 0));

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 2);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: alias).Should().Be(expected: 2);
        budget.Release(lease: lease);
    }

    [Theory]
    [InlineData(data: [GpuVendor.Amd, "amf", "h264_amf"])]
    [InlineData(data: [GpuVendor.Amd, "amf", "hevc_amf"])]
    [InlineData(data: [GpuVendor.Amd, "amf", "av1_amf"])]
    [InlineData(data: [GpuVendor.Intel, "qsv", "h264_qsv"])]
    [InlineData(data: [GpuVendor.Intel, "qsv", "hevc_qsv"])]
    [InlineData(data: [GpuVendor.Intel, "qsv", "vp9_qsv"])]
    [InlineData(data: [GpuVendor.Intel, "vaapi", "h264_vaapi"])]
    [InlineData(data: [GpuVendor.Apple, "videotoolbox", "h264_videotoolbox"])]
    [InlineData(data: [GpuVendor.Apple, "videotoolbox", "hevc_videotoolbox"])]
    public void Vendor_token_and_encoder_name_share_semaphore_across_vendors(
        GpuVendor vendor,
        string vendorToken,
        string encoderName
    )
    {
        // Vendor token + each per-codec encoder name resolve to the same
        // semaphore as the GPU's canonical Name — wrong mapping = wrong
        // worker semaphore + leaked slots.
        GpuDevice gpu = new(
            Vendor: vendor,
            Name: $"{vendor}-test",
            VramMb: 16384,
            MaxEncoderSessions: 2,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);

        ResourceLease leaseViaToken = budget.Acquire(
            requirement: new(GpuDeviceKey: vendorToken, GpuSlots: 1, CpuThreads: 0)
        );

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 1);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: vendorToken).Should().Be(expected: 1);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: encoderName).Should().Be(expected: 1);

        budget.Release(lease: leaseViaToken);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 2);
    }

    [Fact]
    public void Multiple_same_vendor_gpus_first_wins_vendor_token()
    {
        // Two Nvidia GPUs — the vendor token "nvenc" resolves to the FIRST
        // registered GPU's semaphore. Second GPU is reachable only by Name.
        // Pins documented "first-vendor-wins" trade-off.
        GpuDevice firstNvidia = Nvidia(name: "RTX 4090", sessions: 3);
        GpuDevice secondNvidia = Nvidia(name: "RTX 3070", sessions: 2);
        ResourceBudget budget = new(gpuDevices: [firstNvidia, secondNvidia], cpuCores: 8);

        // Acquiring via "nvenc" decreases first GPU, not second.
        ResourceLease lease = budget.Acquire(
            requirement: new(GpuDeviceKey: "nvenc", GpuSlots: 1, CpuThreads: 0)
        );

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "RTX 4090").Should().Be(expected: 2);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "RTX 3070").Should().Be(expected: 2); // untouched
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "nvenc").Should().Be(expected: 2); // alias of first

        budget.Release(lease: lease);
    }

    [Fact]
    public void Different_vendors_get_independent_semaphores()
    {
        // Nvidia + Amd in the same system — acquiring via "nvenc" must NOT
        // affect "amf" availability.
        GpuDevice nv = Nvidia(sessions: 3);
        GpuDevice amd = Amd(sessions: 2);
        ResourceBudget budget = new(gpuDevices: [nv, amd], cpuCores: 8);

        ResourceLease nvLease = budget.Acquire(
            requirement: new(GpuDeviceKey: "nvenc", GpuSlots: 2, CpuThreads: 0)
        );

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "nvenc").Should().Be(expected: 1);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "amf").Should().Be(expected: 2); // unaffected
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: amd.Name).Should().Be(expected: 2);

        budget.Release(lease: nvLease);
    }

    // ── AcquireAsync cancellation rollback ───────────────────────────────────

    [Fact]
    public async Task AcquireAsync_cancellation_after_partial_gpu_release_does_not_leak_slots()
    {
        // Acquire all 3 GPU slots elsewhere. Then call AcquireAsync for 2
        // slots with a cancellation source — the second slot wait will block;
        // cancelling must release the first slot we DID acquire.
        GpuDevice gpu = Nvidia(sessions: 3);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);

        // Soak 2 of 3 slots so AcquireAsync(2 slots) grabs one then blocks.
        ResourceLease soak = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 2, CpuThreads: 0)
        );

        CancellationTokenSource cts = new();
        Task<ResourceLease> waiting = budget.AcquireAsync(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 2, CpuThreads: 0),
            cancellationToken: cts.Token
        );

        // Give the first slot acquire a chance to land then cancel.
        await Task.Delay(millisecondsDelay: 50);
        await cts.CancelAsync();

        Func<Task> act = () => waiting;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // The one slot AcquireAsync DID grab must be released back into the
        // pool — releasing the soak then querying availability should show 3.
        budget.Release(lease: soak);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 3);
    }

    [Fact]
    public async Task AcquireAsync_cancellation_releases_cpu_threads_when_blocked_on_more()
    {
        // 2 CPU cores; soak 1; AcquireAsync for 2 CPU threads will grab the
        // remaining 1, then block. Cancel: must release that 1.
        ResourceBudget budget = new(gpuDevices: [], cpuCores: 2);
        ResourceLease soak = budget.Acquire(requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1));

        CancellationTokenSource cts = new();
        Task<ResourceLease> waiting = budget.AcquireAsync(
            requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 2),
            cancellationToken: cts.Token
        );

        await Task.Delay(millisecondsDelay: 50);
        await cts.CancelAsync();

        Func<Task> act = () => waiting;
        await act.Should().ThrowAsync<OperationCanceledException>();

        budget.Release(lease: soak);
        budget.AvailableCpuThreads().Should().Be(expected: 2);
    }

    // ── TryAcquire rollback paths ────────────────────────────────────────────

    [Fact]
    public void TryAcquire_rolls_back_gpu_slots_when_cpu_acquisition_times_out()
    {
        // 3 GPU slots free, 0 CPU free (soaked). TryAcquire wants 1 GPU +
        // 1 CPU. Must release the GPU it grabbed when CPU times out, so the
        // GPU is still available for the next caller.
        GpuDevice gpu = Nvidia(sessions: 3);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 1);
        ResourceLease cpuSoak = budget.Acquire(requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1));

        ResourceLease? attempt = budget.TryAcquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 1),
            timeout: TimeSpan.FromMilliseconds(milliseconds: 50)
        );

        attempt.Should().BeNull();
        // GPU slot must NOT have been leaked.
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 3);

        budget.Release(lease: cpuSoak);
    }

    [Fact]
    public void TryAcquire_rolls_back_first_gpu_slot_when_second_slot_times_out()
    {
        // 1 GPU slot soaked, 2 free. TryAcquire wants 2 GPU slots — gets one,
        // times out on second, must release the first.
        GpuDevice gpu = Nvidia(sessions: 2);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);
        ResourceLease soak = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 0)
        );

        ResourceLease? attempt = budget.TryAcquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 2, CpuThreads: 0),
            timeout: TimeSpan.FromMilliseconds(milliseconds: 50)
        );

        attempt.Should().BeNull();
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 1); // soak only

        budget.Release(lease: soak);
    }

    // ── Unknown-key behavior ─────────────────────────────────────────────────

    [Fact]
    public void AvailableGpuEncoderSlots_returns_zero_for_unknown_key()
    {
        // Worker probes unknown keys all the time — must NOT throw. Returning
        // 0 is the documented "not registered" signal.
        ResourceBudget budget = new(gpuDevices: [Nvidia()], cpuCores: 8);

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "does_not_exist").Should().Be(expected: 0);
    }

    [Fact]
    public void Acquire_with_unknown_gpu_key_throws_with_available_keys_listed()
    {
        ResourceBudget budget = new(gpuDevices: [Nvidia(name: "RTX 4090")], cpuCores: 8);

        Action act = () =>
            budget.Acquire(requirement: new(GpuDeviceKey: "h264_amf", GpuSlots: 1, CpuThreads: 0));

        act.Should()
            .Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain(expected: "h264_amf")
            .And.Contain(expected: "RTX 4090"); // available keys listed
    }

    // ── Monitor wiring ───────────────────────────────────────────────────────

    [Fact]
    public void CurrentGpuEncodeUtilization_returns_zero_when_monitor_null()
    {
        ResourceBudget budget = new(gpuDevices: [Nvidia()], cpuCores: 8);
        budget.CurrentGpuEncodeUtilization(gpuDeviceKey: "anything").Should().Be(expected: 0.0);
    }

    [Fact]
    public void CurrentGpuEncodeUtilization_delegates_to_monitor_when_set()
    {
        Mock<IResourceMonitor> monitor = new();
        monitor.Setup(expression: m => m.GetGpuEncodeUtilization("rtx-4090")).Returns(value: 0.42);
        ResourceBudget budget = new(gpuDevices: [Nvidia()], cpuCores: 8, monitor: monitor.Object);

        budget.CurrentGpuEncodeUtilization(gpuDeviceKey: "rtx-4090").Should().Be(expected: 0.42);
    }

    // ── Acquire / Release with no resource requirement ──────────────────────

    [Fact]
    public void Acquire_with_zero_slots_and_zero_threads_returns_lease_without_blocking()
    {
        // Zero-requirement acquires are valid synthetic requests (analyze
        // tasks etc.) — must complete instantly with an empty lease.
        ResourceBudget budget = new(gpuDevices: [Nvidia()], cpuCores: 8);

        ResourceLease lease = budget.Acquire(requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 0));

        lease.LeaseId.Should().NotBeNullOrEmpty();
        lease.GpuSlots.Should().Be(expected: 0);
        lease.CpuThreads.Should().Be(expected: 0);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "nvenc").Should().Be(expected: 3);
        budget.AvailableCpuThreads().Should().Be(expected: 8);
    }

    [Fact]
    public void Release_with_zero_slots_does_not_throw()
    {
        ResourceBudget budget = new(gpuDevices: [Nvidia()], cpuCores: 8);
        ResourceLease lease = new(LeaseId: "L1", GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 0);

        Action act = () => budget.Release(lease: lease);
        act.Should().NotThrow();
    }

    // ── IHardwareCapabilities lazy registration ──────────────────────────────

    [Fact]
    public void IHardwareCapabilities_constructor_registers_gpus_from_live_list()
    {
        GpuDevice gpu = Nvidia();
        Mock<IHardwareCapabilities> caps = new();
        caps.Setup(expression: c => c.Gpus).Returns(value: [gpu]);
        caps.Setup(expression: c => c.CpuCores).Returns(value: 8);

        ResourceBudget budget = new(
            hardware: caps.Object,
            monitor: null,
            logger: NullLogger<ResourceBudget>.Instance
        );

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: gpu.MaxEncoderSessions);
        budget.AvailableCpuThreads().Should().Be(expected: 8);
    }

    [Fact]
    public void IHardwareCapabilities_constructor_with_empty_gpus_returns_zero_until_first_acquire()
    {
        // Detection still pending at construction — Gpus is empty. The
        // documented contract is that lazy registration fires on the
        // Acquire path (GetGpuSemaphore miss) — Available* is the cheap
        // "current state" probe and intentionally does NOT trigger it.
        List<GpuDevice> liveList = [];
        Mock<IHardwareCapabilities> caps = new();
        caps.Setup(expression: c => c.Gpus).Returns(valueFunction: () => liveList);
        caps.Setup(expression: c => c.CpuCores).Returns(value: 4);

        ResourceBudget budget = new(
            hardware: caps.Object,
            monitor: null,
            logger: NullLogger<ResourceBudget>.Instance
        );

        budget.AvailableGpuEncoderSlots(gpuDeviceKey: "nvenc").Should().Be(expected: 0);

        GpuDevice gpu = Nvidia();
        liveList.Add(item: gpu);

        // Even after the live list populates, Available alone does not
        // trigger registration — the worker is expected to call Acquire
        // first, which DOES.
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 0);
    }

    [Fact]
    public void IHardwareCapabilities_constructor_with_empty_gpus_then_acquire_triggers_registration()
    {
        // Same lazy path but exercised through Acquire — proves GetGpuSemaphore
        // calls TryRegisterGpus on miss.
        List<GpuDevice> liveList = [];
        Mock<IHardwareCapabilities> caps = new();
        caps.Setup(expression: c => c.Gpus).Returns(valueFunction: () => liveList);
        caps.Setup(expression: c => c.CpuCores).Returns(value: 4);

        ResourceBudget budget = new(
            hardware: caps.Object,
            monitor: null,
            logger: NullLogger<ResourceBudget>.Instance
        );

        GpuDevice gpu = Nvidia();
        liveList.Add(item: gpu);

        ResourceLease lease = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 0)
        );

        lease.GpuSlots.Should().Be(expected: 1);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: gpu.MaxEncoderSessions - 1);
    }

    [Fact]
    public void IsGpuDeviceRegistered_RetriesLazyRegistration_WhenDetectionCompletesLate()
    {
        // Detection hadn't finished at construction (Gpus starts empty), so
        // IsGpuDeviceRegistered must NOT latch a stale "false" — the queue
        // worker's budget gate calls this on every saturated retry, and a
        // legitimate GPU key must eventually read true once the live list
        // populates, the same way GetGpuSemaphore already retries.
        List<GpuDevice> liveList = [];
        Mock<IHardwareCapabilities> caps = new();
        caps.Setup(expression: c => c.Gpus).Returns(valueFunction: () => liveList);
        caps.Setup(expression: c => c.CpuCores).Returns(value: 4);

        ResourceBudget budget = new(
            hardware: caps.Object,
            monitor: null,
            logger: NullLogger<ResourceBudget>.Instance
        );

        GpuDevice gpu = Nvidia();
        budget.IsGpuDeviceRegistered(gpuDeviceKey: gpu.Name).Should().BeFalse();

        liveList.Add(item: gpu);

        budget.IsGpuDeviceRegistered(gpuDeviceKey: gpu.Name).Should().BeTrue();
    }

    // ── Acquire(requirement, timeout) rollback ───────────────────────────────
    //
    // The sync Acquire had no timeout at all (blocked forever) and no
    // rollback — a throw from GetGpuSemaphore or a mid-acquisition failure
    // after GPU slots were already granted leaked them, since the caller
    // never received a ResourceLease to release them with.

    [Fact]
    public void Acquire_WithTimeout_RollsBackGpuSlots_WhenCpuAcquisitionTimesOut()
    {
        // 3 GPU slots free, 0 CPU free (soaked). Acquire wants 1 GPU + 1 CPU
        // with a short timeout. The GPU slot it grabbed must be released when
        // the CPU wait times out, so the GPU stays available for the next
        // caller.
        GpuDevice gpu = Nvidia(sessions: 3);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 1);
        ResourceLease cpuSoak = budget.Acquire(requirement: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1));

        Action act = () =>
            budget.Acquire(
                requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 1),
                timeout: TimeSpan.FromMilliseconds(milliseconds: 50)
            );

        act.Should().Throw<TimeoutException>();
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 3);

        budget.Release(lease: cpuSoak);
    }

    [Fact]
    public void Acquire_WithTimeout_RollsBackFirstGpuSlot_WhenSecondGpuSlotTimesOut()
    {
        // 1 of 2 GPU slots free. Requesting 2 slots grabs the 1 free slot
        // then times out waiting for the second — the first must roll back.
        GpuDevice gpu = Nvidia(sessions: 2);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);
        ResourceLease soak = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 0)
        );

        Action act = () =>
            budget.Acquire(
                requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 2, CpuThreads: 0),
                timeout: TimeSpan.FromMilliseconds(milliseconds: 50)
            );

        act.Should().Throw<TimeoutException>();
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 1);

        budget.Release(lease: soak);
    }

    [Fact]
    public void Acquire_WithTimeout_ThrowsTimeoutException_WhenGpuFullySaturated()
    {
        GpuDevice gpu = Nvidia(sessions: 1);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);
        ResourceLease soak = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 0)
        );

        Action act = () =>
            budget.Acquire(
                requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 0),
                timeout: TimeSpan.FromMilliseconds(milliseconds: 50)
            );

        act.Should().Throw<TimeoutException>().WithMessage(expectedWildcardPattern: "*GPU slot*");

        budget.Release(lease: soak);
    }

    [Fact]
    public void Acquire_ParameterlessOverload_StillGrantsLease_WhenBudgetAvailable()
    {
        // The default-timeout overload must behave exactly like the old
        // unconditional Acquire when the budget is NOT saturated.
        GpuDevice gpu = Nvidia(sessions: 2);
        ResourceBudget budget = new(gpuDevices: [gpu], cpuCores: 4);

        ResourceLease lease = budget.Acquire(
            requirement: new(GpuDeviceKey: gpu.Name, GpuSlots: 1, CpuThreads: 1)
        );

        lease.Should().NotBeNull();
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 1);
        budget.Release(lease: lease);
        budget.AvailableGpuEncoderSlots(gpuDeviceKey: gpu.Name).Should().Be(expected: 2);
    }
}
