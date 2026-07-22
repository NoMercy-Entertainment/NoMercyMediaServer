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

using System.Management;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem.Information;

public static class Gpu
{
    public static List<string> Vendors()
    {
        if (Software.IsWindows)
            return GetGpuVendorsWindows();

        if (Software.IsLinux)
            return GetGpuVendorsLinux();

        if (Software.IsMac)
            return GetGpuVendorsMac();

        return ["Unknown"];
    }

    private static List<string> GetGpuVendorsWindows()
    {
        List<string> vendors = [];
#pragma warning disable CA1416
        try
        {
            using ManagementObjectSearcher searcher = new(queryString: "SELECT * FROM Win32_VideoController");

            foreach (ManagementBaseObject? obj in searcher.Get())
            {
                string vendor = obj[propertyName: "AdapterCompatibility"]?.ToString() ?? "Unknown";
                if (!vendors.Contains(item: vendor))
                    vendors.Add(item: vendor);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(message: $"Error detecting GPUs on Windows: {ex.Message}");
        }
#pragma warning restore CA1416
        return vendors;
    }

    private static List<string> GetGpuVendorsLinux()
    {
        string output = Shell.ExecCommand(command: "lspci | grep -i 'VGA' | awk -F ': ' '{print $2}'");
        return output.Split(separator: '\n').Where(predicate: v => !string.IsNullOrEmpty(value: v)).ToList();
    }

    private static List<string> GetGpuVendorsMac()
    {
        string output = Shell.ExecCommand(
            command: "system_profiler SPDisplaysDataType | grep 'Chipset Model' | awk -F ': ' '{print $2}'"
        );
        return output.Split(separator: '\n').Where(predicate: v => !string.IsNullOrEmpty(value: v)).ToList();
    }

    public static List<string> Names()
    {
        if (Software.IsWindows)
            return GetGpuNamesWindows();

        if (Software.IsLinux)
            return GetGpuNamesLinux();

        if (Software.IsMac)
            return GetGpuNamesMac();

        return ["Unknown"];
    }

    private static List<string> GetGpuNamesWindows()
    {
        List<string> gpus = [];

#pragma warning disable CA1416
        try
        {
            ManagementObjectSearcher searcher = new(queryString: "select Name from Win32_VideoController");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                if (o is not ManagementObject item)
                    continue;
                if (item[propertyName: "Name"] is not { } value)
                    continue;
                if (value.ToString() is not { } valueString)
                    continue;
                gpus.Add(item: valueString.Trim());
            }
        }
        catch (Exception ex)
        {
            Logger.Error(message: $"Error detecting GPUs on Windows: {ex.Message}");
        }
#pragma warning restore CA1416

        return gpus;
    }

    private static List<string> GetGpuNamesLinux()
    {
        string output = Shell.ExecCommand(command: "lspci | grep 'VGA'");
        return output.Split(separator: '\n').Where(predicate: v => !string.IsNullOrEmpty(value: v)).ToList();
    }

    private static List<string> GetGpuNamesMac()
    {
        List<string> gpus = [];

        string systemProfilerOutput = Shell.ExecCommand(command: "system_profiler SPDisplaysDataType");

        string[] lines = systemProfilerOutput.Split(separator: '\n');
        foreach (string line in lines)
        {
            const string marker = "Chipset Model:";
            if (line.TrimStart().StartsWith(value: marker))
            {
                string gpuName = line.Substring(
                        startIndex: line.IndexOf(value: marker, comparisonType: StringComparison.Ordinal) + marker.Length
                    )
                    .Trim();
                if (!string.IsNullOrEmpty(value: gpuName))
                    gpus.Add(item: gpuName);
            }
        }

        return gpus;
    }
}
