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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Tests.Encoder.Infrastructure;

public class ProcessRunnerTests
{
    [Fact]
    public void ProcessResult_Stores_AllFields()
    {
        ProcessResult result = new(
            ExitCode: 0,
            StdOut: "output",
            StdErr: "",
            Duration: TimeSpan.FromSeconds(value: 1.5)
        );

        result.ExitCode.Should().Be(expected: 0);
        result.StdOut.Should().Be(expected: "output");
        result.StdErr.Should().BeEmpty();
        result.Duration.Should().Be(expected: TimeSpan.FromSeconds(value: 1.5));
    }

    [Fact]
    public void ProcessResult_IsSuccess_TrueForZeroExit()
    {
        ProcessResult result = new(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ProcessResult_IsSuccess_FalseForNonZeroExit()
    {
        ProcessResult result = new(ExitCode: 1, StdOut: "", StdErr: "error", Duration: TimeSpan.Zero);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRunner_RunsSimpleCommand()
    {
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        ProcessResult result = await runner.RunAsync(executable: "dotnet", arguments: ["--version"], workingDirectory: (string?)null);

        result.IsSuccess.Should().BeTrue();
        result.StdOut.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRunner_CapturesNonZeroExitCode()
    {
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        // dotnet with an unknown command returns non-zero
        ProcessResult result = await runner.RunAsync(
            executable: "dotnet",
            arguments: ["nonexistent-command-xyz"],
            workingDirectory: (string?)null
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRunner_RespectsTimeout()
    {
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 100));

        // 'dotnet --info' takes a moment — should be cancelled
        Func<Task> act = () => runner.RunAsync(executable: "dotnet", arguments: ["--info"], workingDirectory: null, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── stdout / stderr capture ────────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_CapturesStdOut()
    {
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);

        ProcessResult result = await runner.RunAsync(executable: "dotnet", arguments: ["--version"], workingDirectory: (string?)null);

        result.StdOut.Should().NotBeNullOrWhiteSpace();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRunner_OnStdOutCallback_FiresOncePerLine()
    {
        // The callback overload is what live-encode uses to stream FFmpeg's
        // progress lines. Verify each emitted line lands in the callback.
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        List<string> captured = [];

        ProcessResult result = await runner.RunAsync(
            executable: "dotnet",
            arguments: ["--version"],
            onStdOut: line => captured.Add(item: line),
            onStdErr: null,
            workingDirectory: null,
            cancellationToken: CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeEmpty(because: "at least one line should have been streamed");
        // Builder also accumulated.
        string joined = string.Join(separator: "\n", values: captured);
        joined.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRunner_OnProcessStartedCallback_ReceivesPositivePid()
    {
        // Live transcode wires this to register the PID into ProcessThrottle
        // so it can later be suspended/resumed. Must fire BEFORE the process
        // exits — otherwise the throttle can't act on a short-lived task.
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        int capturedPid = -1;

        await runner.RunAsync(
            executable: "dotnet",
            arguments: ["--version"],
            onStdOut: null,
            onStdErr: null,
            workingDirectory: null,
            cancellationToken: CancellationToken.None,
            killSignal: CancellationToken.None,
            onProcessStarted: pid => capturedPid = pid
        );

        capturedPid.Should().BeGreaterThan(expected: 0);
    }

    // ── working directory ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_RespectsCustomWorkingDirectory()
    {
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"pr-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            ProcessResult result = await runner.RunAsync(
                executable: "dotnet",
                arguments: ["--version"],
                workingDirectory: tempDir,
                cancellationToken: CancellationToken.None
            );

            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessRunner_MissingWorkingDirectory_IsCreatedDefensively()
    {
        // ProcessRunner creates a missing working directory rather than
        // letting Process.Start throw a misleading "could not start process"
        // Win32 error. Regression for an actual production race.
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        string missingDir = Path.Combine(path1: Path.GetTempPath(), path2: $"pr-missing-{Guid.NewGuid():N}");
        Directory.Exists(path: missingDir).Should().BeFalse(because: "the dir must not exist beforehand");

        try
        {
            ProcessResult result = await runner.RunAsync(
                executable: "dotnet",
                arguments: ["--version"],
                workingDirectory: missingDir,
                cancellationToken: CancellationToken.None
            );

            result.IsSuccess.Should().BeTrue();
            Directory.Exists(path: missingDir).Should().BeTrue(because: "the runner must have created it");
        }
        finally
        {
            if (Directory.Exists(path: missingDir))
                Directory.Delete(path: missingDir, recursive: true);
        }
    }

    // ── extra environment variables ────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_ExtraEnvIsPassedThrough()
    {
        // Cross-platform env-var inspection via a tiny dotnet inline script.
        // Use a unique key so the test doesn't depend on inherited state.
        ProcessRunner runner = new(logger: NullLogger<ProcessRunner>.Instance);
        string key = $"NM_TEST_{Guid.NewGuid():N}";

        Dictionary<string, string> env = new() { [key: key] = "hello-world" };

        // Build a small dotnet-script-style snippet that echoes the env var.
        string script =
            "$\"value=' + System.Environment.GetEnvironmentVariable(\"" + key + "\") + '\"";

        // Use platform-appropriate shell to inspect the env. The runner
        // sets the env on the child process, so any shell that echoes it
        // will show the value.
        bool isWindows = OperatingSystem.IsWindows();
        string shell = isWindows ? "cmd" : "sh";
        string[] args = isWindows ? ["/c", $"echo %{key}%"] : ["-c", $"echo ${key}"];

        ProcessResult result = await runner.RunAsync(
            executable: shell,
            arguments: args,
            extraEnv: env,
            workingDirectory: null,
            cancellationToken: CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.StdOut.Should().Contain(expected: "hello-world");
        _ = script; // unused — kept for narrative
    }
}
