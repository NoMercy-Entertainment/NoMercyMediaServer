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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SharpGen.Runtime;
using Vortice.DXGI;

namespace NoMercy.Helpers.Monitoring;

[SupportedOSPlatform("windows")]
internal sealed class WindowsResourceProvider : IResourceProvider, IDisposable
{
    // -----------------------------------------------------------------------
    // DXGI LUID → adapter name cache
    // Built once at construction; LUIDs don't change without a driver reload.
    // -----------------------------------------------------------------------

    // PDH encodes LUID as two hex segments joined by '_':
    //   "0x00000000_0x0001E147"  (HighPart_LowPart)
    // DXGI Luid.HighPart = int, Luid.LowPart = uint.
    // We normalise to uppercase so dictionary lookup is case-insensitive.
    private static readonly Dictionary<string, string> DxgiLuidNames = BuildDxgiLuidNames();

    private static Dictionary<string, string> BuildDxgiLuidNames()
    {
        Dictionary<string, string> map = [];

        try
        {
            IDXGIFactory1? factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            if (factory is null)
                return map;

            using IDXGIFactory1 dxgiFactory = factory;

            uint adapterIndex = 0;
            while (true)
            {
                Result hr = dxgiFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter);
                if (hr.Failure || adapter is null)
                    break;

                using IDXGIAdapter1 dxgiAdapter = adapter;
                AdapterDescription1 desc = dxgiAdapter.Description1;

                // Skip software/fallback adapters (Microsoft Basic Render Driver, WARP, etc.).
                // AdapterFlags.Software is the DXGI_ADAPTER_FLAG_SOFTWARE bit.
                if ((desc.Flags & AdapterFlags.Software) != 0)
                {
                    adapterIndex++;
                    continue;
                }

                // Belt-and-braces name blacklist — driver naming is inconsistent enough
                // that the flag alone can miss edge cases.
                if (
                    desc.Description.StartsWith(
                        "Microsoft Basic",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || desc.Description.Contains(
                        "Basic Render Driver",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    adapterIndex++;
                    continue;
                }

                // Normalise LUID to match PDH format "0xHHHHHHHH_0xLLLLLLLL"
                string luidKey =
                    $"0x{desc.Luid.HighPart:X8}_0x{desc.Luid.LowPart:X8}".ToUpperInvariant();

                map[luidKey] = desc.Description;
                adapterIndex++;
            }
        }
        catch
        {
            // DXGI unavailable — names fall back to "GPU N"
        }

        return map;
    }

    private static string ResolveGpuName(string luid, int fallbackIndex)
    {
        // PDH LUID segments may be lowercase; normalise for lookup.
        string upperLuid = luid.ToUpperInvariant();
        return DxgiLuidNames.TryGetValue(upperLuid, out string? name)
            ? name
            : $"GPU {fallbackIndex}";
    }

    // -----------------------------------------------------------------------
    // CPU counters
    // -----------------------------------------------------------------------

    private PerformanceCounter? _cpuTotal;
    private List<PerformanceCounter> _cpuCores = [];

    // -----------------------------------------------------------------------
    // GPU counters (GPU Engine category — Windows 10 1607+)
    // Kept alive between calls so that NextValue() can return a meaningful
    // delta (the first call always returns 0 — disposing after every read
    // means we never get the second sample).  The list is refreshed every
    // 30 seconds so that processes that start using the GPU after server
    // startup are eventually picked up.
    // -----------------------------------------------------------------------

    private record GpuCounterEntry(string Luid, string EngineType, PerformanceCounter Counter);

    private List<GpuCounterEntry> _gpuCounters = [];
    private DateTime _gpuLastRefresh = DateTime.MinValue;

    // -----------------------------------------------------------------------
    // Memory: P/Invoke
    // -----------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    internal WindowsResourceProvider()
    {
        InitCpuCounters();

        // First read on PerformanceCounter always returns 0 — do a synchronous
        // warm-up so the first real Collect() call returns meaningful data.
        WarmUp();
    }

    private void InitCpuCounters()
    {
        // "% Processor Utility" is what Task Manager uses — it accounts for turbo boost and
        // frequency scaling, giving the most accurate load reading. Values can exceed 100%
        // when turbo is active; we clamp to 0-100 for display purposes.
        // Fall back to "% Processor Time" if "Processor Information" is unavailable.
        try
        {
            _cpuTotal = new("Processor Information", "% Processor Utility", "_Total", true);
        }
        catch
        {
            // Fall back to the standard busy/idle counter if the preferred one is absent
            try
            {
                _cpuTotal = new("Processor", "% Processor Time", "_Total", true);
            }
            catch
            {
                _cpuTotal = null;
            }
        }

        // Per-core counters: "Processor Information" uses "0,0", "0,1", ... instance names.
        // Filter out "_Total" and the aggregate "0,_Total" rollup instances.
        try
        {
            PerformanceCounterCategory category = new("Processor Information");
            string[] instances = category
                .GetInstanceNames()
                .Where(n => n != "_Total" && !n.StartsWith("0,_"))
                .OrderBy(n => n)
                .ToArray();

            foreach (string instance in instances)
            {
                try
                {
                    PerformanceCounter core = new(
                        "Processor Information",
                        "% Processor Utility",
                        instance,
                        true
                    );
                    _cpuCores.Add(core);
                }
                catch
                {
                    // skip individual core if it fails
                }
            }
        }
        catch
        {
            _cpuCores = [];
        }
    }

    // Enumerate GPU Engine instances fresh each call so processes that begin
    // using the GPU after server startup are included.  The caller is
    // responsible for disposing every counter in the returned list.
    private static List<GpuCounterEntry> BuildGpuCounters()
    {
        // "GPU Engine" category exists on Windows 10 1607+ with WDDM 2.x drivers.
        // Instance names look like:
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_0_engtype_3D
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_1_engtype_VideoDecode
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_2_engtype_VideoEncode
        List<GpuCounterEntry> result = [];

        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return result;

            PerformanceCounterCategory category = new("GPU Engine");
            string[] instances = category.GetInstanceNames();

            foreach (string instance in instances)
            {
                string? luid = ExtractSegment(instance, "luid_");
                string? engType = ExtractEngineType(instance);

                if (luid is null || engType is null)
                    continue;

                // We only care about these engine types
                if (
                    engType
                    is not (
                        "3D"
                        or "VideoDecode"
                        or "VideoEncode"
                        or "VideoProcessing"
                        or "Compute"
                    )
                )
                    continue;

                try
                {
                    PerformanceCounter counter = new(
                        "GPU Engine",
                        "Utilization Percentage",
                        instance,
                        true
                    );
                    result.Add(new(luid, engType, counter));
                }
                catch
                {
                    // skip individual instance on failure
                }
            }
        }
        catch
        {
            // return whatever we have so far
        }

        return result;
    }

    // Extract the LUID portion from an instance name.
    // "pid_X_luid_0xHIGH_0xLOW_phys_..." → "0xHIGH_0xLOW"
    private static string? ExtractSegment(string instance, string prefix)
    {
        int start = instance.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += prefix.Length;

        // LUID is two hex segments separated by '_': 0xXXXX_0xXXXX
        string remaining = instance[start..];
        string[] parts = remaining.Split('_');
        if (parts.Length < 2)
            return null;
        return $"{parts[0]}_{parts[1]}";
    }

    // Extract engine type from "..._engtype_3D" or "..._engtype_VideoDecode" etc.
    private static string? ExtractEngineType(string instance)
    {
        const string marker = "_engtype_";
        int idx = instance.LastIndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        return instance[(idx + marker.Length)..];
    }

    private void WarmUp()
    {
        try
        {
            _cpuTotal?.NextValue();
        }
        catch
        { /* ignore */
        }

        foreach (PerformanceCounter c in _cpuCores)
        {
            try
            {
                c.NextValue();
            }
            catch
            { /* ignore */
            }
        }

        // Build GPU counters once and keep them alive in _gpuCounters.
        // Call NextValue() once on each to seed the PDH baseline (always
        // returns 0, but without this first call the second call — the one
        // that actually reports the delta — would also return 0).
        _gpuCounters = BuildGpuCounters();
        _gpuLastRefresh = DateTime.UtcNow;

        foreach (GpuCounterEntry e in _gpuCounters)
        {
            try
            {
                e.Counter.NextValue();
            }
            catch
            { /* ignore */
            }
        }

        // Let Windows PDH accumulate a valid sample interval
        Thread.Sleep(500);
    }

    // -----------------------------------------------------------------------
    // IResourceProvider.Collect
    // -----------------------------------------------------------------------

    public Resource Collect()
    {
        Resource resource = new()
        {
            Cpu = new() { Core = [] },
            Memory = new(),
            _gpu = [],
        };

        CollectCpu(resource);
        CollectMemory(resource);
        CollectGpu(resource);

        return resource;
    }

    private void CollectCpu(Resource resource)
    {
        if (_cpuTotal is null)
            return;

        try
        {
            resource.Cpu.Total = Math.Clamp(Math.Round(_cpuTotal.NextValue(), 1), 0, 100);
        }
        catch
        {
            resource.Cpu.Total = 0;
        }

        double max = 0;

        for (int i = 0; i < _cpuCores.Count; i++)
        {
            try
            {
                double util = Math.Clamp(Math.Round(_cpuCores[i].NextValue(), 1), 0, 100);
                resource.Cpu.Core.Add(new() { Index = i, Utilization = util });
                if (util > max)
                    max = util;
            }
            catch
            {
                resource.Cpu.Core.Add(new() { Index = i, Utilization = 0 });
            }
        }

        resource.Cpu.Max = max;
    }

    private static void CollectMemory(Resource resource)
    {
        MemoryStatusEx status = new() { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };

        if (!GlobalMemoryStatusEx(ref status))
            return;

        const double gb = 1024.0 * 1024.0 * 1024.0;
        resource.Memory.Total = Math.Round(status.ullTotalPhys / gb, 2);
        resource.Memory.Available = Math.Round(status.ullAvailPhys / gb, 2);
        resource.Memory.Use = Math.Round((status.ullTotalPhys - status.ullAvailPhys) / gb, 2);
    }

    private void CollectGpu(Resource resource)
    {
        // Re-enumerate instances if the list is empty or hasn't been refreshed
        // in the last 30 seconds.  This picks up processes that started using
        // the GPU after server startup without re-building counters every cycle.
        bool needsRefresh =
            _gpuCounters.Count == 0 || (DateTime.UtcNow - _gpuLastRefresh).TotalSeconds > 30;

        if (needsRefresh)
        {
            // Dispose every existing counter before discarding the list.
            foreach (GpuCounterEntry e in _gpuCounters)
            {
                try
                {
                    e.Counter.Dispose();
                }
                catch
                { /* ignore */
                }
            }

            _gpuCounters = BuildGpuCounters();
            _gpuLastRefresh = DateTime.UtcNow;

            // Seed the PDH baseline — first NextValue() always returns 0.
            // Return early; the next Collect() cycle will have a valid delta.
            foreach (GpuCounterEntry e in _gpuCounters)
            {
                try
                {
                    e.Counter.NextValue();
                }
                catch
                { /* ignore */
                }
            }

            return;
        }

        if (_gpuCounters.Count == 0)
            return;

        // Read the accumulated delta from every live counter.
        // Remove any that throw (the process they belonged to has exited).
        Dictionary<string, Dictionary<string, double>> totals = [];
        List<GpuCounterEntry> stale = [];

        foreach (GpuCounterEntry entry in _gpuCounters)
        {
            try
            {
                float value = entry.Counter.NextValue();

                if (!totals.TryGetValue(entry.Luid, out Dictionary<string, double>? engines))
                {
                    engines = [];
                    totals[entry.Luid] = engines;
                }

                engines.TryGetValue(entry.EngineType, out double existing);
                engines[entry.EngineType] = existing + value;
            }
            catch
            {
                // Counter is stale — queue it for removal.
                stale.Add(entry);
            }
        }

        // Clean up stale counters without rebuilding the whole list.
        foreach (GpuCounterEntry e in stale)
        {
            try
            {
                e.Counter.Dispose();
            }
            catch
            { /* ignore */
            }

            _gpuCounters.Remove(e);
        }

        // Build Gpu objects, one per LUID (= one per physical GPU).
        //
        // Core uses MAX across engine types rather than SUM.  A process can
        // simultaneously use 3D and VideoEncode; summing would double-count
        // and push the value well above 100%.  Task Manager reports the
        // highest active engine, so we mirror that behaviour here.
        int gpuIndex = 0;
        foreach (KeyValuePair<string, Dictionary<string, double>> kvp in totals)
        {
            // If the LUID is absent from the DXGI map the adapter was filtered out
            // (software/Basic Render Driver).  Don't emit it at all.
            string upperLuid = kvp.Key.ToUpperInvariant();
            if (!DxgiLuidNames.ContainsKey(upperLuid))
                continue;

            Dictionary<string, double> engines = kvp.Value;

            Gpu gpu = new()
            {
                Identifier = $"gpu/{gpuIndex}",
                Name = ResolveGpuName(kvp.Key, gpuIndex),
                D3D = Math.Round(engines.GetValueOrDefault("3D"), 1),
                Decode = Math.Round(engines.GetValueOrDefault("VideoDecode"), 1),
                Encode = Math.Round(engines.GetValueOrDefault("VideoEncode"), 1),
                Core = Math.Round(
                    new[]
                    {
                        engines.GetValueOrDefault("3D"),
                        engines.GetValueOrDefault("Compute"),
                        engines.GetValueOrDefault("VideoProcessing"),
                        engines.GetValueOrDefault("VideoDecode"),
                        engines.GetValueOrDefault("VideoEncode"),
                    }.Max(),
                    1
                ),
                Memory = 0, // not available via PDH GPU Engine category
                Power = 0, // not available via PDH GPU Engine category
            };

            resource._gpu[$"gpu/{gpuIndex}"] = gpu;
            gpuIndex++;
        }
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _cpuTotal?.Dispose();
        _cpuTotal = null;

        foreach (PerformanceCounter c in _cpuCores)
            c.Dispose();
        _cpuCores = [];

        foreach (GpuCounterEntry e in _gpuCounters)
        {
            try
            {
                e.Counter.Dispose();
            }
            catch
            { /* ignore */
            }
        }

        _gpuCounters = [];
    }
}
