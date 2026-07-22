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
    private static string LogFilePath => Path.Combine(path1: AppFiles.AppPath, path2: "launcher.log");

    public LauncherLogTests()
    {
        if (File.Exists(path: LogFilePath))
            File.Delete(path: LogFilePath);
    }

    public void Dispose()
    {
        if (File.Exists(path: LogFilePath))
            File.Delete(path: LogFilePath);
    }

    [Fact]
    public void Info_AppendsLineWithInfoLevelAndMessage()
    {
        LauncherLog.Info(message: "server started");

        string content = File.ReadAllText(path: LogFilePath);
        content.Should().Contain(expected: "[INFO] server started");
    }

    [Fact]
    public void Error_WithoutException_AppendsLineWithErrorLevelAndMessage()
    {
        LauncherLog.Error(message: "stop command failed");

        string content = File.ReadAllText(path: LogFilePath);
        content.Should().Contain(expected: "[ERROR] stop command failed");
        content.Should().NotContain(unexpected: " | ");
    }

    [Fact]
    public void Error_WithException_AppendsMessageAndExceptionDetails()
    {
        InvalidOperationException exception = new(message: "pipe unavailable");

        LauncherLog.Error(message: "stop command failed", ex: exception);

        string content = File.ReadAllText(path: LogFilePath);
        content.Should().Contain(expected: "[ERROR] stop command failed | ");
        content.Should().Contain(expected: "InvalidOperationException");
        content.Should().Contain(expected: "pipe unavailable");
    }

    [Fact]
    public void MultipleCalls_AppendRatherThanOverwrite()
    {
        LauncherLog.Info(message: "first line");
        LauncherLog.Info(message: "second line");

        string[] lines = File.ReadAllLines(path: LogFilePath);
        lines.Should().HaveCount(expected: 2);
        lines[0].Should().Contain(expected: "first line");
        lines[1].Should().Contain(expected: "second line");
    }

    [Fact]
    public void Info_PrefixesLineWithTimestamp()
    {
        LauncherLog.Info(message: "timestamped");

        string line = File.ReadAllLines(path: LogFilePath)[0];

        // yyyy-MM-dd HH:mm:ss — verify the literal shape, not the exact clock value.
        line.Should().MatchRegex(regularExpression: @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] timestamped$");
    }
}
