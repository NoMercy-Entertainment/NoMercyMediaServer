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

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

public class TaskProgressTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // InMemoryTaskProgressStore
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Store_Update_And_Get_RoundTrips()
    {
        InMemoryTaskProgressStore store = new();
        TaskProgressSnapshot snap = MakeSnapshot(id: "t1", percent: 42);

        store.Update(taskId: "t1", snapshot: snap);

        TaskProgressSnapshot? got = store.Get(taskId: "t1");
        got.Should().NotBeNull();
        got!.PercentComplete.Should().Be(expected: 42);
    }

    [Fact]
    public void Store_LatestUpdateWins()
    {
        InMemoryTaskProgressStore store = new();
        store.Update(taskId: "t1", snapshot: MakeSnapshot(id: "t1", percent: 10));
        store.Update(taskId: "t1", snapshot: MakeSnapshot(id: "t1", percent: 80));

        store.Get(taskId: "t1")!.PercentComplete.Should().Be(expected: 80);
    }

    [Fact]
    public void Store_GetAll_FiltersStaleEntries()
    {
        InMemoryTaskProgressStore store = new();
        // Back-date one snapshot past the 15-minute stale window.
        TaskProgressSnapshot stale = MakeSnapshot(id: "old", percent: 50) with
        {
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(value: -30),
        };
        store.Update(taskId: "old", snapshot: stale);
        store.Update(taskId: "fresh", snapshot: MakeSnapshot(id: "fresh", percent: 20));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().ContainSingle(predicate: s => s.TaskId == "fresh");
        all.Should().NotContain(predicate: s => s.TaskId == "old");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HttpTaskProgressSink
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sink_WithoutCoordinator_DoesNothing()
    {
        // Standalone install: no CoordinatorUrl → sink must be a silent
        // no-op, never hit the network.
        EncoderOptions options = new();
        FakeHandler handler = new();
        HttpTaskProgressSink sink = new(
            httpClientFactory: MakeFactory(handler: handler),
            options: options,
            logger: NullLogger<HttpTaskProgressSink>.Instance
        );

        sink.Report(taskId: "t0", progress: MakeProgress());

        // Allow any fire-and-forget task to run.
        Thread.Sleep(millisecondsTimeout: 50);
        handler.RequestCount.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Sink_ThrottlesMultipleReportsWithinInterval()
    {
        // 10 reports in quick succession should produce at most one POST
        // — the throttle window is 2 seconds by default.
        EncoderOptions options = new()
        {
            CoordinatorUrl = "http://coordinator.test",
            WorkerId = "w1",
        };
        FakeHandler handler = new();
        HttpTaskProgressSink sink = new(
            httpClientFactory: MakeFactory(handler: handler),
            options: options,
            logger: NullLogger<HttpTaskProgressSink>.Instance
        );

        for (int i = 0; i < 10; i++)
            sink.Report(taskId: "t0", progress: MakeProgress(percent: i * 10));

        // Wait for any in-flight fire-and-forget task to complete.
        await Task.Delay(millisecondsDelay: 100);

        handler.RequestCount.Should().Be(expected: 1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LocalWorkerDispatcher forwards progress to the sink
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatcher_ForwardsExecutorProgress_ToSink()
    {
        CapturingSink sink = new();
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
                valueFunction: (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string _,
                    CancellationToken _
                ) =>
                {
                    onProgress?.Invoke(obj: MakeProgress(percent: 25));
                    onProgress?.Invoke(obj: MakeProgress(percent: 75));
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            progressSink: sink,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask task = new(
            TaskId: "progress-task",
            Command: new(Executable: "ffmpeg", Arguments: [], WorkingDirectory: null),
            OutputPath: "/out",
            Type: EncodeTaskType.QualityVariant
        );

        await dispatcher.DispatchAsync(tasks: [task], ct: CancellationToken.None);

        sink.Reports.Should().HaveCount(expected: 2);
        sink.Reports[index: 0].TaskId.Should().Be(expected: "progress-task");
        sink.Reports[index: 0].Progress.PercentComplete.Should().Be(expected: 25);
        sink.Reports[index: 1].Progress.PercentComplete.Should().Be(expected: 75);
    }

    [Fact]
    public async Task Dispatcher_SinkThrows_DoesNotFailEncode()
    {
        // A failing progress sink must never bring down the encode.
        ThrowingSink sink = new();
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
                valueFunction: (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string _,
                    CancellationToken _
                ) =>
                {
                    onProgress?.Invoke(obj: MakeProgress());
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            progressSink: sink,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask task = new(
            TaskId: "survivor",
            Command: new(Executable: "ffmpeg", Arguments: [], WorkingDirectory: null),
            OutputPath: "/out",
            Type: EncodeTaskType.QualityVariant
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: [task], ct: CancellationToken.None);

        results[0].Success.Should().BeTrue(because: "sink failure must never fail the encode");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static TaskProgressSnapshot MakeSnapshot(string id, double percent) =>
        new(
            TaskId: id,
            WorkerId: "w1",
            PercentComplete: percent,
            CurrentFps: 60,
            CurrentSpeed: 1.5,
            CurrentStage: "encode",
            ElapsedSeconds: 10,
            EstimatedRemainingSeconds: 20,
            CurrentTimeSeconds: 5,
            DurationSeconds: 30,
            ReceivedAtUtc: DateTime.UtcNow
        );

    private static EncodingProgress MakeProgress(double percent = 50) =>
        new(
            CorrelationId: "corr",
            PercentComplete: percent,
            Elapsed: TimeSpan.FromSeconds(seconds: 5),
            EstimatedRemaining: TimeSpan.FromSeconds(seconds: 5),
            CurrentFps: 30,
            CurrentSpeed: 1.0,
            CurrentStage: "stage",
            CurrentOperation: "op"
        );

    private static IHttpClientFactory MakeFactory(HttpMessageHandler handler)
    {
        ServiceCollection services = new();
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(configure: b => b.ConfigurePrimaryHttpMessageHandler(configureHandler: () => handler));
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private int _requests;

        public int RequestCount => Volatile.Read(location: ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(location: ref _requests);
            return Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.NoContent));
        }
    }

    private sealed class CapturingSink : ITaskProgressSink
    {
        private readonly List<(string TaskId, EncodingProgress Progress)> _reports = [];

        public IReadOnlyList<(string TaskId, EncodingProgress Progress)> Reports
        {
            get
            {
                lock (_reports)
                    return _reports.ToArray();
            }
        }

        public void Report(string taskId, EncodingProgress progress)
        {
            lock (_reports)
                _reports.Add(item: (taskId, progress));
        }
    }

    private sealed class ThrowingSink : ITaskProgressSink
    {
        public void Report(string taskId, EncodingProgress progress) =>
            throw new InvalidOperationException(message: "sink blew up");
    }
}
