﻿using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DeviceId;

namespace NoMercy.NmSystem;

public class Info
{
    public static string DeviceName { get; set; } = Environment.MachineName;
    public static readonly Guid DeviceId = GetDeviceId();
    public static readonly string Os = RuntimeInformation.OSDescription;
    public static readonly string Platform = GetPlatform();
    public static readonly string Architecture = RuntimeInformation.ProcessArchitecture.ToString();
    public static readonly string? Cpu = GetCpuFullName();
    public static readonly string[] Gpu = GetGpuFullName();
    public static readonly string? Version = GetSystemVersion();
    public static readonly DateTime BootTime = GetBootTime();
    public static readonly DateTime StartTime = DateTime.UtcNow;
    public static readonly string ExecSuffix = Platform == "windows" ? ".exe" : "";

    private static Guid GetDeviceId()
    {
        string? generatedId = new DeviceIdBuilder()
            .AddMachineName()
            .AddOsVersion()
            .OnWindows(windows => windows
                .AddProcessorId()
                .AddMotherboardSerialNumber()
                .AddSystemDriveSerialNumber())
            .OnLinux(linux => linux
                .AddMotherboardSerialNumber()
                .AddSystemDriveSerialNumber())
            .OnMac(mac => mac
                .AddSystemDriveSerialNumber()
                .AddPlatformSerialNumber())
            .ToString();

        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(generatedId));

        return new Guid(hash);
    }

    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "mac";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        throw new Exception("Unknown platform");
    }

    private static string[] GetGpuFullName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Name from Win32_VideoController");
            List<string> gpus = [];
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                if (o is not ManagementObject item) continue;
                if (item["Name"] is not {} value) continue;
                if (value.ToString() is not {} valueString) continue;
                gpus.Add(valueString.Trim());
            }

            return gpus.ToArray();
        }

        string output = ExecuteBashCommand("lspci | grep 'VGA'");

        return output.Trim().Split(':').LastOrDefault()?.Trim()?.Split('\n') ?? [];
    }

    private static string? GetCpuFullName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return item["Name"].ToString()?.Trim();
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Unknown";
        }
        else
        {
            string output = ExecuteBashCommand("lscpu | grep 'Model name:'");
            return output.Trim().Split(':')[1].Trim();
        }

        return "Unknown";
    }

    private static string? GetSystemVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Version from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                var item = (ManagementObject)o;
                return item["Version"].ToString();
            }
        }
        else
        {
            string output = ExecuteBashCommand("uname -r");
            return output.Trim();
        }

        return "Unknown";
    }

    private static DateTime GetBootTime()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select LastBootUpTime from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return ManagementDateTimeConverter.ToDateTime(item["LastBootUpTime"].ToString());
            }
        }
        else
        {
            string output = ExecuteBashCommand("uptime -s");
            return DateTime.Parse(output.Trim());
        }

        return DateTime.MinValue;
    }

    private static string ExecuteBashCommand(string command)
    {
        command = command.Replace("\"", "\\\"");
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}