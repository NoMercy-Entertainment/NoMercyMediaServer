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
using NoMercy.Cli.Commands;
using NoMercy.NmSystem.Information;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>start</c>'s server-discovery probe must prefer, in order,
/// an installed sibling binary, then <c>AppFiles.ServerExePath</c>, then a
/// locally-built dev binary (adding <c>--dev</c> when requested), then fall
/// back to <c>dotnet run --project ... -- --dev</c> — and must return null
/// only once every one of those has failed, never throwing for a missing
/// file.
///
/// These call the private static builder methods directly via reflection —
/// each one only probes the filesystem and builds a <see cref="ProcessStartInfo"/>,
/// it never itself calls <c>Process.Start()</c>, so invoking them here cannot
/// spawn a real process. <c>AppFiles.ServerExePath</c> is test-isolated
/// (namespaced under NOMERCY_APP_PATH), so the "production exe" probe is
/// exercised against a real, controllable file. <c>CreateDevBinaryStartInfo</c>
/// probes the REAL <c>src/NoMercy.Service/bin/{Debug,Release}</c> output next
/// to this checkout, so its assertion is written to hold either way rather
/// than assuming a specific build state.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StartCommandStartInfoTests
{
    private static ProcessStartInfo? CreateProductionStartInfo(bool dev) =>
        PrivateReflection.InvokeStatic<ProcessStartInfo?>(
            typeof(StartCommand),
            "CreateProductionStartInfo",
            dev
        );

    private static ProcessStartInfo? CreateInstalledStartInfo(bool dev) =>
        PrivateReflection.InvokeStatic<ProcessStartInfo?>(
            typeof(StartCommand),
            "CreateInstalledStartInfo",
            dev
        );

    private static ProcessStartInfo? CreateDevBinaryStartInfo() =>
        PrivateReflection.InvokeStatic<ProcessStartInfo?>(
            typeof(StartCommand),
            "CreateDevBinaryStartInfo"
        );

    private static ProcessStartInfo? CreateDotnetRunStartInfo() =>
        PrivateReflection.InvokeStatic<ProcessStartInfo?>(
            typeof(StartCommand),
            "CreateDotnetRunStartInfo"
        );

    private static string? FindProjectDirectory(string projectName) =>
        PrivateReflection.InvokeStatic<string?>(
            typeof(StartCommand),
            "FindProjectDirectory",
            projectName
        );

    private static ProcessStartInfo? FindServerStartInfo(bool dev) =>
        PrivateReflection.InvokeStatic<ProcessStartInfo?>(
            typeof(StartCommand),
            "FindServerStartInfo",
            dev
        );

    [Fact]
    public void CreateProductionStartInfo_NoInstalledExe_ReturnsNull()
    {
        string exePath = AppFiles.ServerExePath;
        if (File.Exists(exePath))
            File.Delete(exePath);

        CreateProductionStartInfo(false).Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateProductionStartInfo_InstalledExePresent_ReturnsStartInfo_WithDevFlagWhenRequested(
        bool dev
    )
    {
        string exePath = AppFiles.ServerExePath;
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(exePath, "placeholder");

        try
        {
            ProcessStartInfo? startInfo = CreateProductionStartInfo(dev);

            startInfo.Should().NotBeNull();
            startInfo!.FileName.Should().Be(exePath);
            startInfo.UseShellExecute.Should().BeFalse();
            if (dev)
                startInfo.ArgumentList.Should().Contain("--dev");
            else
                startInfo.ArgumentList.Should().NotContain("--dev");
        }
        finally
        {
            File.Delete(exePath);
        }
    }

    [Fact]
    public void CreateInstalledStartInfo_NoBinaryNextToCurrentProcess_ReturnsNull()
    {
        // Deterministic in any test environment: nothing puts a
        // "NoMercyMediaServer.exe" next to the dotnet/testhost process that
        // runs the test suite.
        CreateInstalledStartInfo(false).Should().BeNull();
    }

    [Fact]
    public void FindProjectDirectory_RealProjectName_ResolvesToSourceTree()
    {
        string? result = FindProjectDirectory("NoMercy.Service");

        result.Should().NotBeNull();
        Directory.Exists(result!).Should().BeTrue();
        Path.GetFileName(result!.TrimEnd(Path.DirectorySeparatorChar))
            .Should()
            .Be("NoMercy.Service");
    }

    [Fact]
    public void FindProjectDirectory_UnknownProjectName_ReturnsNull()
    {
        FindProjectDirectory($"NoMercy.DoesNotExist.{Guid.NewGuid():N}").Should().BeNull();
    }

    [Fact]
    public void CreateDevBinaryStartInfo_MatchesRealBuildOutputState()
    {
        string? serviceDir = FindProjectDirectory("NoMercy.Service");
        serviceDir.Should().NotBeNull();

        string net = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
        string debugPath = Path.Combine([serviceDir!, "bin", "Debug", net, "NoMercyMediaServer" + Info.ExecSuffix]
        );
        string releasePath = Path.Combine([serviceDir!, "bin", "Release", net, "NoMercyMediaServer" + Info.ExecSuffix]
        );

        ProcessStartInfo? result = CreateDevBinaryStartInfo();

        if (File.Exists(debugPath) || File.Exists(releasePath))
        {
            result.Should().NotBeNull();
            result!.ArgumentList.Should().Contain("--dev");
            new[] { debugPath, releasePath }.Should().Contain(result.FileName);
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Fact]
    public void FindServerStartInfo_NoInstalledOrProductionExe_FallsThroughToDevOrDotnetRun()
    {
        // Neither "installed next to the current process" nor
        // AppFiles.ServerExePath resolve in a clean test environment, so this
        // must fall through to whichever of the last two probes wins — never
        // null, since CreateDotnetRunStartInfo always succeeds in this repo.
        string exePath = AppFiles.ServerExePath;
        if (File.Exists(exePath))
            File.Delete(exePath);

        ProcessStartInfo? result = FindServerStartInfo(false);

        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateDotnetRunStartInfo_AlwaysResolvesInThisRepo_ToDotnetRunWithDevFlag()
    {
        // FindProjectDirectory("NoMercy.Service") always succeeds while running
        // inside this repository's tree, so this fallback builder is
        // deterministic here regardless of what has or hasn't been built.
        ProcessStartInfo? result = CreateDotnetRunStartInfo();

        result.Should().NotBeNull();
        result!.FileName.Should().Be("dotnet");
        result
            .ArgumentList.Should()
            .Equal(["run", "--project", FindProjectDirectory("NoMercy.Service")!, "--", "--dev"]);
    }
}
