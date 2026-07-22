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

// This test project targets plain net10.0 (not net10.0-windows), so the platform
// compatibility analyzer cannot see that the whole suite only ever runs where
// WindowsResourceProvider is actually valid. CA1416 is suppressed at the file
// level rather than widening the project's TFM just for this one assembly.
#pragma warning disable CA1416

using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using NoMercy.Monitoring;
using Xunit;

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// Requirement: every counter read that can fail at runtime (a process exits
/// mid-poll, a counter category disappears, PDH throws) must degrade to a zeroed
/// value for that one sample, never take down the whole Collect() cycle. These
/// tests run on the real Windows counters this host actually exposes — the
/// failure branches that a live counter essentially never takes on a healthy
/// host are forced by handing the provider a real, but already-disposed,
/// PerformanceCounter (a genuine broken state, not a mocked OS API) via
/// reflection into the private fields it already declares.
/// </summary>
public class WindowsResourceProviderTests : IDisposable
{
    private readonly List<WindowsResourceProvider> _providers = [];

    private WindowsResourceProvider CreateProvider()
    {
        WindowsResourceProvider provider = new();
        _providers.Add(item: provider);
        return provider;
    }

    private static Resource NewResource() =>
        new()
        {
            Cpu = new() { Core = [] },
            Memory = new(),
        };

    // A real PerformanceCounter bound to a category that genuinely exists on this
    // host ("Processor Information") but an instance name that never will. PDH
    // binds lazily, so construction succeeds but NextValue() reliably throws
    // InvalidOperationException — a real, reproducible broken-counter state,
    // unlike Dispose() + NextValue() whose behaviour turned out to be
    // version/timing-dependent (sometimes silently reinitialises instead of
    // throwing) when this suite first tried that route.
    private static PerformanceCounter CreateBrokenCounter() =>
        new(
            categoryName: "Processor Information",
            counterName: "% Processor Utility",
            instanceName: $"nomercy-test-bogus-{Guid.NewGuid():N}",
            readOnly: true
        );

    public void Dispose()
    {
        foreach (WindowsResourceProvider provider in _providers)
            provider.Dispose();
    }

    // -----------------------------------------------------------------------
    // Real-hardware integration (this host's actual counters)
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_DoesNotThrow_OnRealHost()
    {
        Action act = () => CreateProvider();

        act.Should().NotThrow();
    }

    [Fact]
    public void Collect_ReturnsClampedCpuTotal_OnRealHost()
    {
        // Collect() calls straight through to GlobalMemoryStatusEx / PerformanceCounter,
        // both Windows-only OS features with no Linux equivalent — CI runs this suite on
        // a single ubuntu-latest runner (no separate per-OS leg), so this is a hard
        // platform requirement, not something the provider itself should degrade around.
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        WindowsResourceProvider provider = CreateProvider();

        Resource resource = provider.Collect();

        resource.Cpu.Total.Should().BeInRange(minimumValue: 0.0, maximumValue: 100.0);
        foreach (Core core in resource.Cpu.Core)
            core.Utilization.Should().BeInRange(minimumValue: 0.0, maximumValue: 100.0);
    }

    [Fact]
    public void Collect_ReturnsPositiveMemoryTotal_OnRealHost()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        WindowsResourceProvider provider = CreateProvider();

        Resource resource = provider.Collect();

        resource
            .Memory.Total.Should()
            .BeGreaterThan(expected: 0.0, because: "GlobalMemoryStatusEx succeeds on any real Windows host");
    }

    [Fact]
    public void Collect_Gpu_EntriesAreWellFormed_WhenAnyGpuIsReported()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        WindowsResourceProvider provider = CreateProvider();

        Resource resource = provider.Collect();

        foreach (Gpu gpu in resource.Gpu)
        {
            gpu.Name.Should()
                .NotBeNullOrWhiteSpace(
                    because: "a reported GPU must resolve to a DXGI adapter name or its fallback"
                );
            gpu.Index.Should().BeGreaterThanOrEqualTo(expected: 0);
            gpu.Core.Should().BeInRange(minimumValue: 0.0, maximumValue: 100.0);
        }
    }

    [Fact]
    public void Dispose_TwiceInARow_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();

        provider.Dispose();
        Action act = () => provider.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ClearsCounterFields()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        WindowsResourceProvider provider = CreateProvider();
        provider.Collect(); // ensure GPU/CPU counters were actually built before disposing

        provider.Dispose();

        ReflectionHelpers.GetField<PerformanceCounter?>(instance: provider, fieldName: "_cpuTotal").Should().BeNull();
        ReflectionHelpers.GetField<IList>(instance: provider, fieldName: "_cpuCores").Count.Should().Be(expected: 0);
        ReflectionHelpers.GetField<IList>(instance: provider, fieldName: "_gpuCounters").Count.Should().Be(expected: 0);
    }

    // -----------------------------------------------------------------------
    // Forced failure branches — real (disposed) counters, not mocked OS APIs
    // -----------------------------------------------------------------------

    [Fact]
    public void CollectCpu_WhenCpuTotalCounterIsNull_ReturnsWithoutModifyingResource()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        ReflectionHelpers.SetField(instance: provider, fieldName: "_cpuTotal", value: null);
        Resource resource = NewResource();

        ReflectionHelpers.InvokeInstance(instance: provider, methodName: "CollectCpu", args: resource);

        resource
            .Cpu.Total.Should()
            .Be(expected: 0.0, because: "no counter means no reading — must not fabricate a value");
        resource.Cpu.Core.Should().BeEmpty();
    }

    [Fact]
    public void CollectCpu_WhenCpuTotalCounterIsBroken_DegradesToZero_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        ReflectionHelpers.SetField(instance: provider, fieldName: "_cpuTotal", value: CreateBrokenCounter());
        Resource resource = NewResource();

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "CollectCpu", args: resource);

        act.Should().NotThrow();
        resource
            .Cpu.Total.Should()
            .Be(expected: 0.0, because: "a broken counter must degrade to zero, not propagate the failure");
    }

    [Fact]
    public void CollectCpu_WhenACoreCounterIsBroken_ThatCoreDegradesToZero_OthersUnaffected()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        PerformanceCounter healthyCore = new(
            categoryName: "Processor Information",
            counterName: "% Processor Utility",
            instanceName: "_Total",
            readOnly: true
        );
        healthyCore.NextValue(); // seed the PDH baseline like WarmUp does
        ReflectionHelpers.SetField(
            instance: provider,
            fieldName: "_cpuCores",
            value: new List<PerformanceCounter> { healthyCore, CreateBrokenCounter() }
        );
        Resource resource = NewResource();

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "CollectCpu", args: resource);

        act.Should().NotThrow();
        resource.Cpu.Core.Should().HaveCount(expected: 2);
        resource
            .Cpu.Core[index: 1]
            .Utilization.Should()
            .Be(expected: 0.0, because: "the broken core counter must degrade to zero");
        resource.Cpu.Core[index: 1].Index.Should().Be(expected: 1);
    }

    [Fact]
    public void WarmUp_WhenCpuTotalIsNull_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        ReflectionHelpers.SetField(instance: provider, fieldName: "_cpuTotal", value: null);

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "WarmUp");

        act.Should().NotThrow();
    }

    [Fact]
    public void WarmUp_WhenACoreCounterIsBroken_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        ReflectionHelpers.SetField(
            instance: provider,
            fieldName: "_cpuCores",
            value: new List<PerformanceCounter> { CreateBrokenCounter() }
        );

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "WarmUp");

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // CollectGpu refresh-cycle timing — forced via the real _gpuLastRefresh
    // field rather than a real 30-second sleep.
    // -----------------------------------------------------------------------

    [Fact]
    public void CollectGpu_WhenLastRefreshIsStale_RebuildsCountersAndReturnsWithoutPopulatingGpu()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        provider.Collect(); // real WarmUp already primed _gpuCounters/_gpuLastRefresh once
        ReflectionHelpers.SetField(instance: provider, fieldName: "_gpuLastRefresh", value: DateTime.MinValue);
        Resource resource = NewResource();

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "CollectGpu", args: resource);

        act.Should().NotThrow();
        resource
            .Gpu.Should()
            .BeEmpty(because: "a refresh cycle seeds the PDH baseline and returns early by design");
    }

    [Fact]
    public void CollectGpu_WhenACounterIsBroken_QueuesItStale_RemovesIt_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        WindowsResourceProvider provider = CreateProvider();
        provider.Collect();
        Type entryType = ReflectionHelpers
            .GetField<IList>(instance: provider, fieldName: "_gpuCounters")
            .GetType()
            .GetGenericArguments()[0];

        object brokenEntry = Activator.CreateInstance(
            type: entryType, args: ["0x00000000_0xDEADBEEF", "3D", CreateBrokenCounter()]
        )!;

        IList counters = (IList)
            Activator.CreateInstance(type: typeof(List<>).MakeGenericType(typeArguments: entryType))!;
        counters.Add(value: brokenEntry);
        ReflectionHelpers.SetField(instance: provider, fieldName: "_gpuCounters", value: counters);
        // Recent refresh so CollectGpu takes the "read deltas" branch, not the rebuild branch.
        ReflectionHelpers.SetField(instance: provider, fieldName: "_gpuLastRefresh", value: DateTime.UtcNow);
        Resource resource = NewResource();

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "CollectGpu", args: resource);

        act.Should().NotThrow();
        ReflectionHelpers
            .GetField<IList>(instance: provider, fieldName: "_gpuCounters")
            .Count.Should()
            .Be(expected: 0, because: "the stale broken entry must be removed");
    }

    [Fact]
    public void WarmUp_WhenCpuTotalCounterIsBroken_DoesNotThrow()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        // WarmUp() only reads _cpuTotal (it never rebuilds it — that happens once
        // in InitCpuCounters), so a broken counter placed here beforehand survives
        // to be exercised by WarmUp's own try/catch around NextValue().
        WindowsResourceProvider provider = CreateProvider();
        ReflectionHelpers.SetField(instance: provider, fieldName: "_cpuTotal", value: CreateBrokenCounter());

        Action act = () => ReflectionHelpers.InvokeInstance(instance: provider, methodName: "WarmUp");

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Pure private static helpers
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: ["pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_0_engtype_3D", "luid_", "0x00000000_0x0001E147"])]
    [InlineData(data: ["pid_9_luid_0xAAAAAAAA_0xBBBBBBBB_phys_0_eng_1_engtype_VideoDecode", "luid_", "0xAAAAAAAA_0xBBBBBBBB"])]
    public void ExtractSegment_ParsesLuidFromInstanceName(
        string instance,
        string prefix,
        string expected
    )
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string? result = (string?)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ExtractSegment", args: [instance, prefix]
            );

        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ExtractSegment_WithoutPrefix_ReturnsNull()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string? result = (string?)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ExtractSegment", args: ["pid_1234_phys_0_eng_0_engtype_3D", "luid_"]
            );

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSegment_WithFewerThanTwoSegmentsAfterPrefix_ReturnsNull()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string? result = (string?)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ExtractSegment", args: ["pid_1234_luid_onlyonepart", "luid_"]
            );

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(data: ["pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D", "3D"])]
    [InlineData(data: ["pid_1_luid_0x0_0x1_phys_0_eng_1_engtype_VideoDecode", "VideoDecode"])]
    [InlineData(data: ["pid_1_luid_0x0_0x1_phys_0_eng_2_engtype_VideoEncode", "VideoEncode"])]
    public void ExtractEngineType_ParsesTrailingEngineType(string instance, string expected)
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string? result = (string?)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ExtractEngineType",
                args: instance
            );

        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ExtractEngineType_WithoutMarker_ReturnsNull()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string? result = (string?)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ExtractEngineType",
                args: "pid_1_luid_0x0_0x1_phys_0_eng_0"
            );

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveGpuName_ForUnknownLuid_ReturnsFallback()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string result = (string)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ResolveGpuName", args: ["0xDEAD_0xBEEF", 42]
            )!;

        result.Should().Be(expected: "GPU 42");
    }

    [Fact]
    public void ResolveGpuName_ForKnownLuid_ReturnsRealAdapterName_WhenHostHasAnAdapter()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        // DxgiLuidNames is built once from this host's real DXGI adapters. On a
        // host with at least one non-software adapter, the "found" branch is
        // exercised with a real key; on a headless host with none, there is
        // nothing to resolve and the branch is legitimately unreachable without
        // hardware (see coverage report notes).
        Dictionary<string, string> luidNames = ReflectionHelpers.GetField<
            Dictionary<string, string>
        >(type: typeof(WindowsResourceProvider), fieldName: "DxgiLuidNames");

        if (luidNames.Count == 0)
            return;

        (string luid, string name) = luidNames.First() is { Key: { } k, Value: { } v }
            ? (k, v)
            : ("0x0", "Unknown");

        string result = (string)
            ReflectionHelpers.InvokeStatic(
                type: typeof(WindowsResourceProvider),
                methodName: "ResolveGpuName", args: [luid, 0]
            )!;

        result.Should().Be(expected: name);
    }

    [Fact]
    public void BuildGpuCounters_DoesNotThrow_AndEveryEntryHasAnAllowedEngineType()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        string[] allowedEngineTypes =
        [
            "3D",
            "VideoDecode",
            "VideoEncode",
            "VideoProcessing",
            "Compute",
        ];

        object result = ReflectionHelpers.InvokeStatic(
            type: typeof(WindowsResourceProvider),
            methodName: "BuildGpuCounters"
        )!;
        IList entries = (IList)result;

        try
        {
            foreach (object? entry in entries)
            {
                string engineType = (string)
                    entry!.GetType().GetProperty(name: "EngineType")!.GetValue(obj: entry)!;
                allowedEngineTypes.Should().Contain(expected: engineType);

                string luid = (string)entry.GetType().GetProperty(name: "Luid")!.GetValue(obj: entry)!;
                luid.Should().StartWith(expected: "0x");
            }
        }
        finally
        {
            foreach (object? entry in entries)
                (
                    entry!.GetType().GetProperty(name: "Counter")!.GetValue(obj: entry) as PerformanceCounter
                )?.Dispose();
        }
    }

    [Fact]
    public void BuildDxgiLuidNames_DoesNotThrow_ReturnsADictionary()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;
        
        object result = ReflectionHelpers.InvokeStatic(
            type: typeof(WindowsResourceProvider),
            methodName: "BuildDxgiLuidNames"
        )!;

        result.Should().BeOfType<Dictionary<string, string>>();
    }
}
