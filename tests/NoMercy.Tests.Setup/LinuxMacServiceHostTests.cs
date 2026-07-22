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

using System.Runtime.InteropServices;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.Setup;

public class LinuxServiceHostTests
{
    [Fact]
    public void GetExecutablePath_ReturnsNonEmptyString()
    {
        string path = AutoStartupManager.GetExecutablePath();
        Assert.False(condition: string.IsNullOrEmpty(value: path), userMessage: "Executable path should not be empty");
    }

    [Fact]
    public void GenerateSystemdUnit_ContainsRequiredSections()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "[Unit]", actualString: content);
        Assert.Contains(expectedSubstring: "[Service]", actualString: content);
        Assert.Contains(expectedSubstring: "[Install]", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasNotifyServiceType()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        // Type=notify is required for sd_notify integration with Microsoft.Extensions.Hosting.Systemd
        Assert.Contains(expectedSubstring: "Type=notify", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_PassesServiceFlag()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "--service", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasNetworkDependency()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "After=network-online.target", actualString: content);
        Assert.Contains(expectedSubstring: "Wants=network-online.target", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasRestartPolicy()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "Restart=on-failure", actualString: content);
        Assert.Contains(expectedSubstring: "RestartSec=10", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasJournalLogging()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "StandardOutput=journal", actualString: content);
        Assert.Contains(expectedSubstring: "StandardError=journal", actualString: content);
        Assert.Contains(expectedSubstring: "SyslogIdentifier=nomercy-mediaserver", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_TargetsUserDefault()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        // User service should target default.target, not multi-user.target
        Assert.Contains(expectedSubstring: "WantedBy=default.target", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_ContainsExecutablePath()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();
        string exePath = AutoStartupManager.GetExecutablePath();

        Assert.Contains(expectedSubstring: $"ExecStart={exePath}", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasWorkingDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "WorkingDirectory=", actualString: content);
    }

    [Fact]
    public void GenerateSystemdUnit_HasDescription()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "Description=NoMercy MediaServer", actualString: content);
    }

    [Fact]
    public void GetSystemdUnitPath_PointsToUserServiceDir()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        string path = AutoStartupManager.GetSystemdUnitPath();

        Assert.EndsWith(expectedEndString: "systemd/user/nomercy-mediaserver.service", actualString: path);
    }

    [Fact]
    public void GetSystemdUnitPath_RespectsXdgConfigHome()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        string? original = Environment.GetEnvironmentVariable(variable: "XDG_CONFIG_HOME");
        try
        {
            string customConfig = "/tmp/test-xdg-config";
            Environment.SetEnvironmentVariable(variable: "XDG_CONFIG_HOME", value: customConfig);

            string path = AutoStartupManager.GetSystemdUnitPath();
            Assert.StartsWith(expectedStartString: customConfig, actualString: path);
            Assert.EndsWith(expectedEndString: "nomercy-mediaserver.service", actualString: path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "XDG_CONFIG_HOME", value: original);
        }
    }

    [Fact]
    public void GenerateSystemdUnit_PathMatchesGetSystemdUnitPath()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string _, string generatedPath) = AutoStartupManager.GenerateSystemdUnit();
        string directPath = AutoStartupManager.GetSystemdUnitPath();

        Assert.Equal(expected: directPath, actual: generatedPath);
    }

    [Fact]
    public void GenerateSystemdUnit_HasDotnetRootEnvironment()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return;

        (string content, string _) = AutoStartupManager.GenerateSystemdUnit();

        Assert.Contains(expectedSubstring: "Environment=DOTNET_ROOT=", actualString: content);
    }

    [Fact]
    public void IsEnabled_ReturnsBool()
    {
        // IsEnabled should return a bool without throwing on any platform
        bool result = AutoStartupManager.IsEnabled();
        Assert.IsType<bool>(@object: result);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenNotRegistered()
    {
        // On a fresh test environment, auto-start should not be registered
        bool result = AutoStartupManager.IsEnabled();
        Assert.False(condition: result);
    }
}
