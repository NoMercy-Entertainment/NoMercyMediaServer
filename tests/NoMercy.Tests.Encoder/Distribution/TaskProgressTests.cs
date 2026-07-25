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
        TaskProgressSnapshot snap = MakeSnapshot("t1", 42);

        store.Update("t1", snap);

        TaskProgressSnapshot? got = store.Get("t1");
        got.Should().NotBeNull();
        got!.PercentComplete.Should().Be(42);
    }

    [Fact]
    public void Store_LatestUpdateWins()
    {
        InMemoryTaskProgressStore store = new();
        store.Update("t1", MakeSnapshot("t1", 10));
        store.Update("t1", MakeSnapshot("t1", 80));

        store.Get("t1")!.PercentComplete.Should().Be(80);
    }

    [Fact]
    public void Store_GetAll_FiltersStaleEntries()
    {
        InMemoryTaskProgressStore store = new();
        // Back-date one snapshot past the 15-minute stale window.
        TaskProgressSnapshot stale = MakeSnapshot("old", 50) with
        {
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-30),
        };
        store.Update("old", stale);
        store.Update("fresh", MakeSnapshot("fresh", 20));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().ContainSingle(s => s.TaskId == "fresh");
        all.Should().NotContain(s => s.TaskId == "old");
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
            MakeFactory(handler),
            options,
            NullLogger<HttpTaskProgressSink>.Instance
        );

        sink.Report("t0", MakeProgress());

        // Allow any fire-and-forget task to run.
        Thread.Sleep(50);
        handler.RequestCount.Should().Be(0);
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
            MakeFactory(handler),
            options,
            NullLogger<HttpTaskProgressSink>.Instance
        );

        for (int i = 0; i < 10; i++)
            sink.Report("t0", MakeProgress(i * 10));

        // Wait for any in-flight fire-and-forget task to complete.
        await Task.Delay(100);

        handler.RequestCount.Should().Be(1);
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
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string _,
                    CancellationToken _
                ) =>
                {
                    onProgress?.Invoke(MakeProgress(25));
                    onProgress?.Invoke(MakeProgress(75));
                    return Task.FromResult(
                        new ExecutionResult(
                            true,
                            0,
                            "",
                            TimeSpan.FromSeconds(1),
                            null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            sink,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask task = new(
            "progress-task",
            new("ffmpeg", [], null),
            "/out",
            EncodeTaskType.QualityVariant
        );

        await dispatcher.DispatchAsync([task], CancellationToken.None);

        sink.Reports.Should().HaveCount(2);
        sink.Reports[0].TaskId.Should().Be("progress-task");
        sink.Reports[0].Progress.PercentComplete.Should().Be(25);
        sink.Reports[1].Progress.PercentComplete.Should().Be(75);
    }

    [Fact]
    public async Task Dispatcher_SinkThrows_DoesNotFailEncode()
    {
        // A failing progress sink must never bring down the encode.
        ThrowingSink sink = new();
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string _,
                    CancellationToken _
                ) =>
                {
                    onProgress?.Invoke(MakeProgress());
                    return Task.FromResult(
                        new ExecutionResult(
                            true,
                            0,
                            "",
                            TimeSpan.FromSeconds(1),
                            null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            sink,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        EncodeTask task = new(
            "survivor",
            new("ffmpeg", [], null),
            "/out",
            EncodeTaskType.QualityVariant
        );

        DispatchResult[] results = await dispatcher.DispatchAsync([task], CancellationToken.None);

        results[0].Success.Should().BeTrue("sink failure must never fail the encode");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static TaskProgressSnapshot MakeSnapshot(string id, double percent) =>
        new(
            id,
            "w1",
            percent,
            60,
            1.5,
            "encode",
            10,
            20,
            5,
            30,
            DateTime.UtcNow
        );

    private static EncodingProgress MakeProgress(double percent = 50) =>
        new(
            "corr",
            percent,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            30,
            1.0,
            "stage",
            "op"
        );

    private static IHttpClientFactory MakeFactory(HttpMessageHandler handler)
    {
        ServiceCollection services = new();
        services
            .AddHttpClient()
            .ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private int _requests;

        public int RequestCount => Volatile.Read(ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
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
                _reports.Add((taskId, progress));
        }
    }

    private sealed class ThrowingSink : ITaskProgressSink
    {
        public void Report(string taskId, EncodingProgress progress) =>
            throw new InvalidOperationException("sink blew up");
    }
}
