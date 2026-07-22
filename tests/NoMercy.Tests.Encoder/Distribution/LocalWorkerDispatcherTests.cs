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
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

public class LocalWorkerDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ReportsSuccessForEachTask_WhenExecutorSucceeds()
    {
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(seconds: 3),
                    Error: null
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask[] tasks = [MakeTask(id: "task-0", outputPath: "/out/a.m3u8"), MakeTask(id: "task-1", outputPath: "/out/b.m3u8")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results.Should().HaveCount(expected: 2);
        results.Should().AllSatisfy(expected: r => r.Success.Should().BeTrue());
        results[0].TaskId.Should().Be(expected: "task-0");
        results[1].TaskId.Should().Be(expected: "task-1");
        executor.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 2)
        );
    }

    [Fact]
    public async Task DispatchAsync_RunsTasksSequentially()
    {
        List<string> callOrder = [];
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                valueFunction: async (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? _,
                    string? corrId,
                    CancellationToken _
                ) =>
                {
                    callOrder.Add(item: $"start:{corrId}");
                    await Task.Delay(millisecondsDelay: 20);
                    callOrder.Add(item: $"end:{corrId}");
                    return new(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromSeconds(seconds: 1),
                        Error: null
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask[] tasks = [MakeTask(id: "t0", outputPath: "/out/a"), MakeTask(id: "t1", outputPath: "/out/b")];

        await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        callOrder.Should().Equal(expected: ["start:t0", "end:t0", "start:t1", "end:t1"]);
    }

    [Fact]
    public async Task DispatchAsync_CapturesFailure_ContinuesWithRemainingTasks()
    {
        Mock<IFfmpegExecutor> executor = new();
        int call = 0;
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: () =>
            {
                int current = ++call;
                bool failFirst = current == 1;
                return Task.FromResult(
                    result: new ExecutionResult(
                        Success: !failFirst,
                        ExitCode: failFirst ? 1 : 0,
                        StdErr: failFirst ? "boom" : "",
                        Duration: TimeSpan.FromSeconds(seconds: 1),
                        Error: failFirst
                            ? new EncodingError(
                                Kind: EncodingErrorKind.ProcessCrashed,
                                Message: "ffmpeg died",
                                FfmpegStderr: null,
                                StageName: "Execute",
                                Recoverable: false
                            )
                            : null
                    )
                );
            });

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask[] tasks = [MakeTask(id: "t0", outputPath: "/out/a"), MakeTask(id: "t1", outputPath: "/out/b")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "ffmpeg died");
        results[1].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_RespectsCancellation_BetweenTasks()
    {
        Mock<IFfmpegExecutor> executor = new();
        int call = 0;
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: () =>
            {
                call++;
                return Task.FromResult(
                    result: new ExecutionResult(
                        Success: true,
                        ExitCode: 0,
                        StdErr: "",
                        Duration: TimeSpan.FromMilliseconds(milliseconds: 10),
                        Error: null
                    )
                );
            });

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask[] tasks =
        [
            MakeTask(id: "t0", outputPath: "/out/a"),
            MakeTask(id: "t1", outputPath: "/out/b"),
            MakeTask(id: "t2", outputPath: "/out/c"),
        ];

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => dispatcher.DispatchAsync(tasks: tasks, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AvailableWorkerCount_IsOne()
    {
        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: new Mock<IFfmpegExecutor>().Object);

        dispatcher.AvailableWorkerCount.Should().Be(expected: 1);
    }

    private static LocalWorkerDispatcher NewDispatcher(IFfmpegExecutor executor) =>
        new(executor: executor, logger: NullLogger<LocalWorkerDispatcher>.Instance);

    private static EncodeTask MakeTask(string id, string outputPath) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: outputPath,
            Type: EncodeTaskType.QualityVariant
        );
}
