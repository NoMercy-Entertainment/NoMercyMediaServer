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

public static class Cpu
{
    internal static List<string> Vendors()
    {
        if (Software.IsWindows)
            return GetCpuVendorsWindows();

        if (Software.IsLinux)
            return GetCpuVendorsLinux();

        if (Software.IsMac)
            return GetCpuVendorsMac();

        return ["Unknown"];
    }

    private static List<string> GetCpuVendorsWindows()
    {
        List<string> vendors = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new(queryString: "select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item[propertyName: "Name"] is string cpuName)
                vendors.Add(item: cpuName.Trim());
        }
#pragma warning restore CA1416

        return vendors;
    }

    private static List<string> GetCpuVendorsLinux()
    {
        List<string> vendors = [];

        string output = Shell.ExecCommand(command: "lscpu");
        string modelName = "Unknown";
        int sockets = 1;

        foreach (string line in output.Split(separator: '\n'))
        {
            if (line.StartsWith(value: "Model name:"))
                modelName = line.Split(separator: ':', count: 2)[1].Trim();
            else if (line.StartsWith(value: "Socket(s):"))
                int.TryParse(s: line.Split(separator: ':', count: 2)[1].Trim(), result: out sockets);
        }

        for (int i = 0; i < sockets; i++)
            vendors.Add(item: modelName);

        return vendors;
    }

    private static List<string> GetCpuVendorsMac()
    {
        List<string> vendors = [];

        string output = Shell.ExecCommand(command: "sysctl -n machdep.cpu.brand_string");
        vendors.Add(item: output.Trim());

        return vendors;
    }

    internal static List<string> Names()
    {
        if (Software.IsWindows)
            return GetCpuNamesWindows();

        if (Software.IsLinux)
            return GetCpuNamesLinux();

        if (Software.IsMac)
            return GetCpuNamesMac();

        return ["Unknown"];
    }

    private static List<string> GetCpuNamesWindows()
    {
        List<string> cpus = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new(queryString: "select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item[propertyName: "Name"] is string cpuName)
                cpus.Add(item: cpuName.Trim());
        }
#pragma warning restore CA1416

        return cpus;
    }

    private static List<string> GetCpuNamesLinux()
    {
        List<string> cpus = [];

        string output = Shell.ExecCommand(command: "lscpu");
        string modelName = "Unknown";
        int sockets = 1;

        foreach (string line in output.Split(separator: '\n'))
        {
            if (line.StartsWith(value: "Model name:"))
                modelName = line.Split(separator: ':', count: 2)[1].Trim();
            else if (line.StartsWith(value: "Socket(s):"))
                int.TryParse(s: line.Split(separator: ':', count: 2)[1].Trim(), result: out sockets);
        }

        for (int i = 0; i < sockets; i++)
            cpus.Add(item: modelName);

        return cpus;
    }

    private static List<string> GetCpuNamesMac()
    {
        List<string> cpus = [];

        string output = Shell.ExecCommand(command: "sysctl -n machdep.cpu.brand_string");
        cpus.Add(item: output.Trim());

        return cpus;
    }
}
