using System.Diagnostics;
using System.Runtime.Versioning;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Helpers.Monitoring;

[SupportedOSPlatform("linux")]
internal sealed class LinuxResourceProvider : IResourceProvider
{
    // -----------------------------------------------------------------------
    // CPU: delta between two /proc/stat reads
    // -----------------------------------------------------------------------

    private record CpuSnapshot(
        string Label,
        long User,
        long Nice,
        long System,
        long Idle,
        long IoWait,
        long Irq,
        long SoftIrq,
        long Steal
    )
    {
        internal long Total => User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal;
        internal long Busy => Total - Idle - IoWait;
    }

    private List<CpuSnapshot> _previousSnapshots = [];

    internal LinuxResourceProvider()
    {
        // Take initial snapshot so the first Collect() has a meaningful delta
        _previousSnapshots = ReadCpuSnapshots();
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

    // -----------------------------------------------------------------------
    // CPU
    // -----------------------------------------------------------------------

    private void CollectCpu(Resource resource)
    {
        List<CpuSnapshot> current = ReadCpuSnapshots();

        if (_previousSnapshots.Count == 0)
        {
            _previousSnapshots = current;
            return;
        }

        // first entry is the aggregate "cpu" line
        CpuSnapshot? aggCurrent = current.FirstOrDefault(s => s.Label == "cpu");
        CpuSnapshot? aggPrev = _previousSnapshots.FirstOrDefault(s => s.Label == "cpu");

        if (aggCurrent is not null && aggPrev is not null)
        {
            resource.Cpu.Total = Math.Round(CalculatePercent(aggPrev, aggCurrent), 1);
        }

        double max = 0;
        int coreIndex = 0;

        foreach (
            CpuSnapshot curr in current.Where(s => s.Label.StartsWith("cpu") && s.Label != "cpu")
        )
        {
            CpuSnapshot? prev = _previousSnapshots.FirstOrDefault(s => s.Label == curr.Label);
            double util = prev is null ? 0 : Math.Round(CalculatePercent(prev, curr), 1);

            resource.Cpu.Core.Add(new Core { Index = coreIndex, Utilization = util });
            if (util > max)
                max = util;
            coreIndex++;
        }

        resource.Cpu.Max = max;
        _previousSnapshots = current;
    }

    private static double CalculatePercent(CpuSnapshot prev, CpuSnapshot curr)
    {
        long totalDelta = curr.Total - prev.Total;
        if (totalDelta <= 0)
            return 0;
        long busyDelta = curr.Busy - prev.Busy;
        return (double)busyDelta / totalDelta * 100.0;
    }

    private static List<CpuSnapshot> ReadCpuSnapshots()
    {
        List<CpuSnapshot> snapshots = [];

        try
        {
            string[] lines = File.ReadAllLines("/proc/stat");
            foreach (string line in lines)
            {
                if (!line.StartsWith("cpu"))
                    break;
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;

                snapshots.Add(
                    new CpuSnapshot(
                        Label: parts[0],
                        User: ParseLong(parts, 1),
                        Nice: ParseLong(parts, 2),
                        System: ParseLong(parts, 3),
                        Idle: ParseLong(parts, 4),
                        IoWait: ParseLong(parts, 5),
                        Irq: ParseLong(parts, 6),
                        SoftIrq: ParseLong(parts, 7),
                        Steal: ParseLong(parts, 8)
                    )
                );
            }
        }
        catch
        {
            // /proc/stat not available (container without procfs, etc.)
        }

        return snapshots;
    }

    private static long ParseLong(string[] parts, int index) =>
        index < parts.Length && long.TryParse(parts[index], out long v) ? v : 0;

    // -----------------------------------------------------------------------
    // Memory
    // -----------------------------------------------------------------------

    private static void CollectMemory(Resource resource)
    {
        try
        {
            Dictionary<string, long> fields = [];
            foreach (string line in File.ReadAllLines("/proc/meminfo"))
            {
                string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                    continue;
                string key = parts[0];
                // value is "N kB"
                string[] valueParts = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (long.TryParse(valueParts[0], out long kb))
                    fields[key] = kb;
            }

            const double gbFactor = 1024.0 * 1024.0; // kB → GB
            long totalKb = fields.GetValueOrDefault("MemTotal");
            long availableKb = fields.GetValueOrDefault("MemAvailable");
            long usedKb = totalKb - availableKb;

            resource.Memory.Total = Math.Round(totalKb / gbFactor, 2);
            resource.Memory.Available = Math.Round(availableKb / gbFactor, 2);
            resource.Memory.Use = Math.Round(usedKb / gbFactor, 2);
        }
        catch
        {
            // /proc/meminfo not available
        }
    }

    // -----------------------------------------------------------------------
    // GPU
    // -----------------------------------------------------------------------

    private static void CollectGpu(Resource resource)
    {
        // Try Nvidia first via nvidia-smi
        if (TryCollectNvidiaGpu(resource))
            return;

        // Try AMD via sysfs
        TryCollectAmdGpu(resource);
    }

    private static bool TryCollectNvidiaGpu(Resource resource)
    {
        try
        {
            using Process proc = new();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments =
                    "--query-gpu=index,utilization.gpu,utilization.memory,utilization.encoder,utilization.decoder,power.draw"
                    + " --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return false;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 6)
                    continue;
                if (!int.TryParse(parts[0], out int index))
                    continue;

                string key = $"gpu/{index}";
                resource._gpu[key] = new Gpu
                {
                    Identifier = key,
                    Core = ParseDouble(parts[1]),
                    Memory = ParseDouble(parts[2]),
                    Encode = ParseDouble(parts[3]),
                    Decode = ParseDouble(parts[4]),
                    D3D = ParseDouble(parts[1]), // map overall utilisation to D3D as well
                    Power = ParseDouble(parts[5]),
                };
            }

            return resource._gpu.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCollectAmdGpu(Resource resource)
    {
        try
        {
            // AMD exposes gpu_busy_percent per card under /sys/class/drm
            string[] cardPaths = Directory
                .GetDirectories("/sys/class/drm", "card*")
                .Where(p => !p.Contains('-')) // skip card0-eDP-1 etc.
                .OrderBy(p => p)
                .ToArray();

            int index = 0;
            foreach (string cardPath in cardPaths)
            {
                string busyFile = Path.Combine(cardPath, "device", "gpu_busy_percent");
                if (!File.Exists(busyFile))
                    continue;

                string content = File.ReadAllText(busyFile).Trim();
                if (!double.TryParse(content, out double utilization))
                    continue;

                string key = $"gpu/{index}";
                resource._gpu[key] = new Gpu
                {
                    Identifier = key,
                    Core = utilization,
                    D3D = utilization,
                };
                index++;
            }
        }
        catch
        {
            // sysfs not available or no AMD GPU
        }
    }

    private static double ParseDouble(string s) =>
        double.TryParse(s, out double v) ? Math.Round(v, 1) : 0;
}
