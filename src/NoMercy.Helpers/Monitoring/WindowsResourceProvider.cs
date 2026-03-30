using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Helpers.Monitoring;

[SupportedOSPlatform("windows")]
internal sealed class WindowsResourceProvider : IResourceProvider, IDisposable
{
    // -----------------------------------------------------------------------
    // CPU counters
    // -----------------------------------------------------------------------

    private PerformanceCounter? _cpuTotal;
    private List<PerformanceCounter> _cpuCores = [];

    // -----------------------------------------------------------------------
    // GPU counters (GPU Engine category — Windows 10 1607+)
    // We enumerate all instances once and bucket them by LUID.
    // -----------------------------------------------------------------------

    private record GpuCounterEntry(string Luid, string EngineType, PerformanceCounter Counter);

    private List<GpuCounterEntry> _gpuCounters = [];

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
        InitGpuCounters();

        // First read on PerformanceCounter always returns 0 — do a synchronous
        // warm-up so the first real Collect() call returns meaningful data.
        WarmUp();
    }

    private void InitCpuCounters()
    {
        try
        {
            _cpuTotal = new PerformanceCounter(
                "Processor Information",
                "% Processor Utility",
                "_Total",
                true
            );
        }
        catch
        {
            // Fall back to the older counter name on some Windows builds
            try
            {
                _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            }
            catch
            {
                _cpuTotal = null;
            }
        }

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

    private void InitGpuCounters()
    {
        // "GPU Engine" category exists on Windows 10 1607+ with WDDM 2.x drivers.
        // Instance names look like:
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_0_engtype_3D
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_1_engtype_VideoDecode
        //   pid_1234_luid_0x00000000_0x0001E147_phys_0_eng_2_engtype_VideoEncode
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return;

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
                    _gpuCounters.Add(new GpuCounterEntry(luid, engType, counter));
                }
                catch
                {
                    // skip
                }
            }
        }
        catch
        {
            _gpuCounters = [];
        }
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
            resource.Cpu.Total = Math.Round(_cpuTotal.NextValue(), 1);
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
                double util = Math.Round(_cpuCores[i].NextValue(), 1);
                resource.Cpu.Core.Add(new Core { Index = i, Utilization = util });
                if (util > max)
                    max = util;
            }
            catch
            {
                resource.Cpu.Core.Add(new Core { Index = i, Utilization = 0 });
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
        if (_gpuCounters.Count == 0)
            return;

        // Aggregate utilisation per LUID × engine type
        Dictionary<string, Dictionary<string, double>> totals = [];

        foreach (GpuCounterEntry entry in _gpuCounters)
        {
            float value;
            try
            {
                value = entry.Counter.NextValue();
            }
            catch
            {
                continue;
            }

            if (!totals.TryGetValue(entry.Luid, out Dictionary<string, double>? engines))
            {
                engines = [];
                totals[entry.Luid] = engines;
            }

            engines.TryGetValue(entry.EngineType, out double existing);
            engines[entry.EngineType] = existing + value;
        }

        // Build Gpu objects, one per LUID (= one per physical GPU)
        int gpuIndex = 0;
        foreach (KeyValuePair<string, Dictionary<string, double>> kvp in totals)
        {
            string luid = kvp.Key;
            Dictionary<string, double> engines = kvp.Value;

            Gpu gpu = new()
            {
                Identifier = $"gpu/{gpuIndex}",
                D3D = Math.Round(engines.GetValueOrDefault("3D"), 1),
                Decode = Math.Round(engines.GetValueOrDefault("VideoDecode"), 1),
                Encode = Math.Round(engines.GetValueOrDefault("VideoEncode"), 1),
                Core = Math.Round(
                    engines.GetValueOrDefault("3D")
                        + engines.GetValueOrDefault("Compute")
                        + engines.GetValueOrDefault("VideoProcessing"),
                    1
                ),
                Memory = 0, // not available via PDH GPU Engine category
                Power = 0, // not available via PDH GPU Engine category
            };

            // Replace the LUID key with a simple index-based key so Gpu.Index works
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
            e.Counter.Dispose();
        _gpuCounters = [];
    }
}
