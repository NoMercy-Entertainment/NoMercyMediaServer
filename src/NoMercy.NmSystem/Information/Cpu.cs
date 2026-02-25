using System.Management;

namespace NoMercy.NmSystem.Information;

public static class Cpu
{
    internal static List<string> Vendors()
    {
        if (Software.IsWindows) return GetCpuVendorsWindows();

        if (Software.IsLinux) return GetCpuVendorsLinux();

        if (Software.IsMac) return GetCpuVendorsMac();

        return ["Unknown"];
    }

    private static List<string> GetCpuVendorsWindows()
    {
        List<string> vendors = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["Name"] is string cpuName) vendors.Add(cpuName.Trim());
        }
#pragma warning restore CA1416

        return vendors;
    }

    private static List<string> GetCpuVendorsLinux()
    {
        List<string> vendors = [];

        string output = SystemCalls.Shell.ExecCommand("lscpu");
        string modelName = "Unknown";
        int sockets = 1;

        foreach (string line in output.Split('\n'))
        {
            if (line.StartsWith("Model name:"))
                modelName = line.Split(':', 2)[1].Trim();
            else if (line.StartsWith("Socket(s):"))
                int.TryParse(line.Split(':', 2)[1].Trim(), out sockets);
        }

        for (int i = 0; i < sockets; i++)
            vendors.Add(modelName);

        return vendors;
    }

    private static List<string> GetCpuVendorsMac()
    {
        List<string> vendors = [];

        string output = SystemCalls.Shell.ExecCommand("sysctl -n machdep.cpu.brand_string");
        vendors.Add(output.Trim());

        return vendors;
    }


    internal static List<string> Names()
    {
        if (Software.IsWindows) return GetCpuNamesWindows();

        if (Software.IsLinux) return GetCpuNamesLinux();

        if (Software.IsMac) return GetCpuNamesMac();

        return ["Unknown"];
    }

    private static List<string> GetCpuNamesWindows()
    {
        List<string> cpus = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["Name"] is string cpuName) cpus.Add(cpuName.Trim());
        }
#pragma warning restore CA1416

        return cpus;
    }

    private static List<string> GetCpuNamesLinux()
    {
        List<string> cpus = [];

        string output = SystemCalls.Shell.ExecCommand("lscpu");
        string modelName = "Unknown";
        int sockets = 1;

        foreach (string line in output.Split('\n'))
        {
            if (line.StartsWith("Model name:"))
                modelName = line.Split(':', 2)[1].Trim();
            else if (line.StartsWith("Socket(s):"))
                int.TryParse(line.Split(':', 2)[1].Trim(), out sockets);
        }

        for (int i = 0; i < sockets; i++)
            cpus.Add(modelName);

        return cpus;
    }

    private static List<string> GetCpuNamesMac()
    {
        List<string> cpus = [];

        string output = SystemCalls.Shell.ExecCommand("sysctl -n machdep.cpu.brand_string");
        cpus.Add(output.Trim());

        return cpus;
    }
}