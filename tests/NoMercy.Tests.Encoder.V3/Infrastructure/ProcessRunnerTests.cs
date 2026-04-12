namespace NoMercy.Tests.Encoder.V3.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.V3.Infrastructure;

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
        Func<Task> act = () => runner.RunAsync("dotnet", ["--info"], (string?)null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
