using System.Runtime.InteropServices;
using NoMercy.NmSystem;

namespace NoMercy.Tests.Setup;

public class LinuxServiceHostTests
{
    [Fact]
    public void GetExecutablePath_ReturnsNonEmptyString()
    {
        string path = AutoStartupManager.GetExecutablePath();
        Assert.False(string.IsNullOrEmpty(path), "Executable path should not be empty");
    }

    [Fact]
    public void GenerateSystemdUnit_ContainsRequiredSections()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("[Unit]", content);
        Assert.Contains("[Service]", content);
        Assert.Contains("[Install]", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasNotifyServiceType()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        // Type=notify is required for sd_notify integration with Microsoft.Extensions.Hosting.Systemd
        Assert.Contains("Type=notify", content);
    }

    [Fact]
    public void GenerateSystemdUnit_PassesServiceFlag()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("--service", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasNetworkDependency()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("After=network-online.target", content);
        Assert.Contains("Wants=network-online.target", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasRestartPolicy()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("Restart=on-failure", content);
        Assert.Contains("RestartSec=10", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasJournalLogging()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("StandardOutput=journal", content);
        Assert.Contains("StandardError=journal", content);
        Assert.Contains("SyslogIdentifier=nomercy-mediaserver", content);
    }

    [Fact]
    public void GenerateSystemdUnit_TargetsUserDefault()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        // User service should target default.target, not multi-user.target
        Assert.Contains("WantedBy=default.target", content);
    }

    [Fact]
    public void GenerateSystemdUnit_ContainsExecutablePath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();
        string exePath = AutoStartupManager.GetExecutablePath();

        Assert.Contains($"ExecStart={exePath}", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasWorkingDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("WorkingDirectory=", content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasDescription()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("Description=NoMercy MediaServer", content);
    }

    [Fact]
    public void GetSystemdUnitPath_PointsToUserServiceDir()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        string path = AutoStartupManager.GetSystemdUnitPath();

        Assert.EndsWith("systemd/user/nomercy-mediaserver.service", path);
    }

    [Fact]
    public void GetSystemdUnitPath_RespectsXdgConfigHome()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        string? original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            string customConfig = "/tmp/test-xdg-config";
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", customConfig);

            string path = AutoStartupManager.GetSystemdUnitPath();
            Assert.StartsWith(customConfig, path);
            Assert.EndsWith("nomercy-mediaserver.service", path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
        }
    }

    [Fact]
    public void GenerateSystemdUnit_PathMatchesGetSystemdUnitPath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string _, string generatedPath) = AutoStartupManager.GenerateSystemdUnit();
        string directPath = AutoStartupManager.GetSystemdUnitPath();

        Assert.Equal(directPath, generatedPath);
    }

    [Fact]
    public void GenerateSystemdUnit_HasDotnetRootEnvironment()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains("Environment=DOTNET_ROOT=", content);
    }

    [Fact]
    public void IsEnabled_ReturnsBool()
    {
        // IsEnabled should return a bool without throwing on any platform
        bool result = AutoStartupManager.IsEnabled();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenNotRegistered()
    {
        // On a fresh test environment, auto-start should not be registered
        bool result = AutoStartupManager.IsEnabled();
        Assert.False(result);
    }
}
