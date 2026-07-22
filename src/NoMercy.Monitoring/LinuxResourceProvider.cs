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
using System.Globalization;
using System.Runtime.Versioning;

namespace NoMercy.Monitoring;

[SupportedOSPlatform(platformName: "linux")]
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

        CollectCpu(resource: resource);
        CollectMemory(resource: resource);
        CollectGpu(resource: resource);

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
        CpuSnapshot? aggCurrent = current.FirstOrDefault(predicate: s => s.Label == "cpu");
        CpuSnapshot? aggPrev = _previousSnapshots.FirstOrDefault(predicate: s => s.Label == "cpu");

        if (aggCurrent is not null && aggPrev is not null)
        {
            resource.Cpu.Total = Math.Round(value: CalculatePercent(prev: aggPrev, curr: aggCurrent), digits: 1);
        }

        double max = 0;
        int coreIndex = 0;

        foreach (
            CpuSnapshot curr in current.Where(predicate: s => s.Label.StartsWith(value: "cpu") && s.Label != "cpu")
        )
        {
            CpuSnapshot? prev = _previousSnapshots.FirstOrDefault(predicate: s => s.Label == curr.Label);
            double util = prev is null ? 0 : Math.Round(value: CalculatePercent(prev: prev, curr: curr), digits: 1);

            resource.Cpu.Core.Add(item: new() { Index = coreIndex, Utilization = util });
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
            string[] lines = File.ReadAllLines(path: "/proc/stat");
            foreach (string line in lines)
            {
                if (!line.StartsWith(value: "cpu"))
                    break;
                string[] parts = line.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                    continue;

                snapshots.Add(
                    item: new(
                        Label: parts[0],
                        User: ParseLong(parts: parts, index: 1),
                        Nice: ParseLong(parts: parts, index: 2),
                        System: ParseLong(parts: parts, index: 3),
                        Idle: ParseLong(parts: parts, index: 4),
                        IoWait: ParseLong(parts: parts, index: 5),
                        Irq: ParseLong(parts: parts, index: 6),
                        SoftIrq: ParseLong(parts: parts, index: 7),
                        Steal: ParseLong(parts: parts, index: 8)
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
        index < parts.Length && long.TryParse(s: parts[index], result: out long v) ? v : 0;

    // -----------------------------------------------------------------------
    // Memory
    // -----------------------------------------------------------------------

    private static void CollectMemory(Resource resource)
    {
        try
        {
            Dictionary<string, long> fields = [];
            foreach (string line in File.ReadAllLines(path: "/proc/meminfo"))
            {
                string[] parts = line.Split(separator: ':', options: StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                    continue;
                string key = parts[0];
                // value is "N kB"
                string[] valueParts = parts[1].Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);
                if (long.TryParse(s: valueParts[0], result: out long kb))
                    fields[key: key] = kb;
            }

            const double gbFactor = 1024.0 * 1024.0; // kB → GB
            long totalKb = fields.GetValueOrDefault(key: "MemTotal");
            long availableKb = fields.GetValueOrDefault(key: "MemAvailable");
            long usedKb = totalKb - availableKb;

            resource.Memory.Total = Math.Round(value: totalKb / gbFactor, digits: 2);
            resource.Memory.Available = Math.Round(value: availableKb / gbFactor, digits: 2);
            resource.Memory.Use = Math.Round(value: usedKb / gbFactor, digits: 2);
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
        if (TryCollectNvidiaGpu(resource: resource))
            return;

        // Try AMD via sysfs
        TryCollectAmdGpu(resource: resource);
    }

    private static bool TryCollectNvidiaGpu(Resource resource)
    {
        try
        {
            using Process proc = new();
            proc.StartInfo = new()
            {
                FileName = "nvidia-smi",
                Arguments =
                    "--query-gpu=index,utilization.gpu,utilization.memory,utilization.encoder,utilization.decoder,power.draw,name"
                    + " --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(milliseconds: 3000);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(value: output))
                return false;

            foreach (string line in output.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split(separator: ',', options: StringSplitOptions.TrimEntries);
                if (parts.Length < 7)
                    continue;
                if (!int.TryParse(s: parts[0], result: out int index))
                    continue;

                string key = $"gpu/{index}";
                resource._gpu[key: key] = new()
                {
                    Identifier = key,
                    Name = parts[6],
                    Core = ParseDouble(s: parts[1]),
                    Memory = ParseDouble(s: parts[2]),
                    Encode = ParseDouble(s: parts[3]),
                    Decode = ParseDouble(s: parts[4]),
                    D3D = ParseDouble(s: parts[1]), // map overall utilisation to D3D as well
                    Power = ParseDouble(s: parts[5]),
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
                .GetDirectories(path: "/sys/class/drm", searchPattern: "card*")
                .Where(predicate: p => !p.Contains(value: '-')) // skip card0-eDP-1 etc.
                .OrderBy(keySelector: p => p)
                .ToArray();

            int index = 0;
            foreach (string cardPath in cardPaths)
            {
                string busyFile = Path.Combine(path1: cardPath, path2: "device", path3: "gpu_busy_percent");
                if (!File.Exists(path: busyFile))
                    continue;

                string content = File.ReadAllText(path: busyFile).Trim();
                if (!double.TryParse(s: content, provider: CultureInfo.InvariantCulture, result: out double utilization))
                    continue;

                string key = $"gpu/{index}";
                string amdName = ReadAmdGpuName(cardPath: cardPath, fallbackIndex: index);
                resource._gpu[key: key] = new()
                {
                    Identifier = key,
                    Name = amdName,
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

    // AMD GPU name from /sys/class/drm/cardN/device/product_name, with fallback.
    private static string ReadAmdGpuName(string cardPath, int fallbackIndex)
    {
        try
        {
            string namePath = Path.Combine(path1: cardPath, path2: "device", path3: "product_name");
            if (File.Exists(path: namePath))
            {
                string name = File.ReadAllText(path: namePath).Trim();
                if (!string.IsNullOrWhiteSpace(value: name))
                    return name;
            }
        }
        catch
        {
            // sysfs read failed — fall through to fallback
        }

        return $"GPU {fallbackIndex}";
    }

    // nvidia-smi's CSV output is always period-decimal regardless of the host OS
    // locale — parsing with the current culture would misread e.g. "55.55" as
    // 5555 on a comma-decimal locale (nl-NL, de-DE, ...), corrupting every GPU
    // utilization/power reading on a non-en-US server.
    private static double ParseDouble(string s) =>
        double.TryParse(s: s, provider: CultureInfo.InvariantCulture, result: out double v) ? Math.Round(value: v, digits: 1) : 0;
}
