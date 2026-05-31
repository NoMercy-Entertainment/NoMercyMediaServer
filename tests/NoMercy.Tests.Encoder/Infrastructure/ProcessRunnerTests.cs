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
            Duration: TimeSpan.FromSeconds(1.5)
        );

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Be("output");
        result.StdErr.Should().BeEmpty();
        result.Duration.Should().Be(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void ProcessResult_IsSuccess_TrueForZeroExit()
    {
        ProcessResult result = new(0, "", "", TimeSpan.Zero);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ProcessResult_IsSuccess_FalseForNonZeroExit()
    {
        ProcessResult result = new(1, "", "error", TimeSpan.Zero);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRunner_RunsSimpleCommand()
    {
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        ProcessResult result = await runner.RunAsync("dotnet", ["--version"], (string?)null);

        result.IsSuccess.Should().BeTrue();
        result.StdOut.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRunner_CapturesNonZeroExitCode()
    {
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        // dotnet with an unknown command returns non-zero
        ProcessResult result = await runner.RunAsync(
            "dotnet",
            ["nonexistent-command-xyz"],
            (string?)null
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRunner_RespectsTimeout()
    {
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

        // 'dotnet --info' takes a moment — should be cancelled
        Func<Task> act = () => runner.RunAsync("dotnet", ["--info"], null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── stdout / stderr capture ────────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_CapturesStdOut()
    {
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);

        ProcessResult result = await runner.RunAsync("dotnet", ["--version"], (string?)null);

        result.StdOut.Should().NotBeNullOrWhiteSpace();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRunner_OnStdOutCallback_FiresOncePerLine()
    {
        // The callback overload is what live-encode uses to stream FFmpeg's
        // progress lines. Verify each emitted line lands in the callback.
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        List<string> captured = [];

        ProcessResult result = await runner.RunAsync(
            "dotnet",
            ["--version"],
            onStdOut: line => captured.Add(line),
            onStdErr: null,
            workingDirectory: null,
            cancellationToken: CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeEmpty("at least one line should have been streamed");
        // Builder also accumulated.
        string joined = string.Join("\n", captured);
        joined.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRunner_OnProcessStartedCallback_ReceivesPositivePid()
    {
        // Live transcode wires this to register the PID into ProcessThrottle
        // so it can later be suspended/resumed. Must fire BEFORE the process
        // exits — otherwise the throttle can't act on a short-lived task.
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        int capturedPid = -1;

        await runner.RunAsync(
            "dotnet",
            ["--version"],
            onStdOut: null,
            onStdErr: null,
            workingDirectory: null,
            cancellationToken: CancellationToken.None,
            killSignal: CancellationToken.None,
            onProcessStarted: pid => capturedPid = pid
        );

        capturedPid.Should().BeGreaterThan(0);
    }

    // ── working directory ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_RespectsCustomWorkingDirectory()
    {
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        string tempDir = Path.Combine(Path.GetTempPath(), $"pr-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ProcessResult result = await runner.RunAsync(
                "dotnet",
                ["--version"],
                tempDir,
                CancellationToken.None
            );

            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessRunner_MissingWorkingDirectory_IsCreatedDefensively()
    {
        // ProcessRunner creates a missing working directory rather than
        // letting Process.Start throw a misleading "could not start process"
        // Win32 error. Regression for an actual production race.
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        string missingDir = Path.Combine(Path.GetTempPath(), $"pr-missing-{Guid.NewGuid():N}");
        Directory.Exists(missingDir).Should().BeFalse("the dir must not exist beforehand");

        try
        {
            ProcessResult result = await runner.RunAsync(
                "dotnet",
                ["--version"],
                missingDir,
                CancellationToken.None
            );

            result.IsSuccess.Should().BeTrue();
            Directory.Exists(missingDir).Should().BeTrue("the runner must have created it");
        }
        finally
        {
            if (Directory.Exists(missingDir))
                Directory.Delete(missingDir, recursive: true);
        }
    }

    // ── extra environment variables ────────────────────────────────────────

    [Fact]
    public async Task ProcessRunner_ExtraEnvIsPassedThrough()
    {
        // Cross-platform env-var inspection via a tiny dotnet inline script.
        // Use a unique key so the test doesn't depend on inherited state.
        ProcessRunner runner = new(NullLogger<ProcessRunner>.Instance);
        string key = $"NM_TEST_{Guid.NewGuid():N}";

        Dictionary<string, string> env = new() { [key] = "hello-world" };

        // Build a small dotnet-script-style snippet that echoes the env var.
        string script =
            "$\"value=' + System.Environment.GetEnvironmentVariable(\"" + key + "\") + '\"";

        // Use platform-appropriate shell to inspect the env. The runner
        // sets the env on the child process, so any shell that echoes it
        // will show the value.
        bool isWindows = OperatingSystem.IsWindows();
        string shell = isWindows ? "cmd" : "sh";
        string[] args = isWindows ? ["/c", $"echo %{key}%"] : ["-c", $"echo $\"{key}\""];

        ProcessResult result = await runner.RunAsync(
            shell,
            args,
            extraEnv: env,
            workingDirectory: null,
            cancellationToken: CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.StdOut.Should().Contain("hello-world");
        _ = script; // unused — kept for narrative
    }
}
