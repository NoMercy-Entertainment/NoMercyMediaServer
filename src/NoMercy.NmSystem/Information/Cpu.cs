using System.Management;

namespace NoMercy.NmSystem.Information;

public class Cpu
{
    internal static List<string> Vendors()
    {
        if (Software.IsWindows)
        {
            return GetCpuVendorsWindows();
        }

        if (Software.IsLinux)
        {
            return GetCpuVendorsLinux();
        }

        if (Software.IsMac)
        {
            return GetCpuVendorsMac();
        }
        
        return ["Unknown"];
    }
    private static List<string> GetCpuVendorsWindows()
    {
        List<string> vendors = [];
        
        ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["Name"] is string cpuName)
            {
                vendors.Add(cpuName.Trim());
            }
        }

        return vendors;
    }
    private static List<string> GetCpuVendorsLinux()
    {
        List<string> vendors = [];
        
        string output = Helper.RunCommand("lscpu | grep 'Model name:'");
        vendors.Add(output.Trim().Split(':')[1].Trim());

        return vendors;
    }
    private static List<string> GetCpuVendorsMac()
    {
        List<string> vendors = [];
        
        string output = Helper.RunCommand("sysctl -n machdep.cpu.brand_string");
        vendors.Add(output.Trim());

        return vendors;
    }
    

    internal static List<string> Names()
    {
        if (Software.IsWindows)
        {
            return GetCpuNamesWindows();
        }

        if (Software.IsLinux)
        {
            return GetCpuNamesLinux();
        }

        if (Software.IsMac)
        {
            return GetCpuNamesMac();
        }
        
        return ["Unknown"];
    }
    private static List<string> GetCpuNamesWindows()
    {
        List<string> cpus = [];
        ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["Name"] is string cpuName)
            {
                cpus.Add(cpuName.Trim());
            }
        }

        return cpus;
    }
    private static List<string> GetCpuNamesLinux()
    {
        List<string> cpus = [];
        
        string output = Helper.RunCommand("lscpu | grep 'Model name:'");
        cpus.Add(output.Trim().Split(':')[1].Trim());
        
        return cpus;
    }
    private static List<string> GetCpuNamesMac()
    {
        List<string> cpus = [];
        
        string output = Helper.RunCommand("sysctl -n machdep.cpu.brand_string");
        cpus.Add(output.Trim());
        
        return cpus;
    }
}