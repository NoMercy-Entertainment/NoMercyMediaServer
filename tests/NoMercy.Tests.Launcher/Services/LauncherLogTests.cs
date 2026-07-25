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

using NoMercy.Launcher.Services;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Launcher.Services;

/// <summary>
/// <see cref="LauncherLog"/> writes real lines to <c>launcher.log</c> under the
/// (test-isolated, see TestEnvironmentSetup) AppFiles.AppPath. This is the only
/// diagnostic trail available once the Launcher's tray process has no console —
/// the format and append-only behavior are the actual requirement, not just
/// "doesn't throw".
/// </summary>
public sealed class LauncherLogTests : IDisposable
{
    private static string LogFilePath => Path.Combine(AppFiles.AppPath, "launcher.log");

    public LauncherLogTests()
    {
        if (File.Exists(LogFilePath))
            File.Delete(LogFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(LogFilePath))
            File.Delete(LogFilePath);
    }

    [Fact]
    public void Info_AppendsLineWithInfoLevelAndMessage()
    {
        LauncherLog.Info("server started");

        string content = File.ReadAllText(LogFilePath);
        content.Should().Contain("[INFO] server started");
    }

    [Fact]
    public void Error_WithoutException_AppendsLineWithErrorLevelAndMessage()
    {
        LauncherLog.Error("stop command failed");

        string content = File.ReadAllText(LogFilePath);
        content.Should().Contain("[ERROR] stop command failed");
        content.Should().NotContain(" | ");
    }

    [Fact]
    public void Error_WithException_AppendsMessageAndExceptionDetails()
    {
        InvalidOperationException exception = new("pipe unavailable");

        LauncherLog.Error("stop command failed", exception);

        string content = File.ReadAllText(LogFilePath);
        content.Should().Contain("[ERROR] stop command failed | ");
        content.Should().Contain("InvalidOperationException");
        content.Should().Contain("pipe unavailable");
    }

    [Fact]
    public void MultipleCalls_AppendRatherThanOverwrite()
    {
        LauncherLog.Info("first line");
        LauncherLog.Info("second line");

        string[] lines = File.ReadAllLines(LogFilePath);
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("first line");
        lines[1].Should().Contain("second line");
    }

    [Fact]
    public void Info_PrefixesLineWithTimestamp()
    {
        LauncherLog.Info("timestamped");

        string line = File.ReadAllLines(LogFilePath)[0];

        // yyyy-MM-dd HH:mm:ss — verify the literal shape, not the exact clock value.
        line.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] timestamped$");
    }
}
