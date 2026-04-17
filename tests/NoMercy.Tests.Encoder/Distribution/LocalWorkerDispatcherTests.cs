namespace NoMercy.Tests.Encoder.Distribution;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;

public class LocalWorkerDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ReportsSuccessForEachTask_WhenExecutorSucceeds()
    {
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(3),
                    Error: null
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask[] tasks = [MakeTask("task-0", "/out/a.m3u8"), MakeTask("task-1", "/out/b.m3u8")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        results[0].TaskId.Should().Be("task-0");
        results[1].TaskId.Should().Be("task-1");
        executor.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task DispatchAsync_RunsTasksSequentially()
    {
        List<string> callOrder = [];
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<NoMercy.Encoder.Progress.EncodingProgress>? _,
                    string? corrId,
                    CancellationToken _
                ) =>
                {
                    callOrder.Add($"start:{corrId}");
                    await Task.Delay(20);
                    callOrder.Add($"end:{corrId}");
                    return new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromSeconds(1),
                        Error: null
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask[] tasks = [MakeTask("t0", "/out/a"), MakeTask("t1", "/out/b")];

        await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        callOrder.Should().Equal("start:t0", "end:t0", "start:t1", "end:t1");
    }

    [Fact]
    public async Task DispatchAsync_CapturesFailure_ContinuesWithRemainingTasks()
    {
        Mock<IFfmpegExecutor> executor = new();
        int call = 0;
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                int current = ++call;
                bool failFirst = current == 1;
                return Task.FromResult(
                    new ExecutionResult(
                        Success: !failFirst,
                        ExitCode: failFirst ? 1 : 0,
                        StdErr: failFirst ? "boom" : "",
                        Duration: TimeSpan.FromSeconds(1),
                        Error: failFirst
                            ? new EncodingError(
                                EncodingErrorKind.ProcessCrashed,
                                "ffmpeg died",
                                null,
                                "Execute",
                                Recoverable: false
                            )
                            : null
                    )
                );
            });

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask[] tasks = [MakeTask("t0", "/out/a"), MakeTask("t1", "/out/b")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("ffmpeg died");
        results[1].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_RespectsCancellation_BetweenTasks()
    {
        Mock<IFfmpegExecutor> executor = new();
        int call = 0;
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<NoMercy.Encoder.Progress.EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                call++;
                return Task.FromResult(
                    new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromMilliseconds(10),
                        Error: null
                    )
                );
            });

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask[] tasks =
        [
            MakeTask("t0", "/out/a"),
            MakeTask("t1", "/out/b"),
            MakeTask("t2", "/out/c"),
        ];

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => dispatcher.DispatchAsync(tasks, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AvailableWorkerCount_IsOne()
    {
        LocalWorkerDispatcher dispatcher = NewDispatcher(new Mock<IFfmpegExecutor>().Object);

        dispatcher.AvailableWorkerCount.Should().Be(1);
    }

    private static LocalWorkerDispatcher NewDispatcher(IFfmpegExecutor executor) =>
        new(executor, NullLogger<LocalWorkerDispatcher>.Instance);

    private static EncodeTask MakeTask(string id, string outputPath) =>
        new(
            TaskId: id,
            Command: new FfmpegCommand("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: outputPath,
            Type: EncodeTaskType.QualityVariant
        );
}
