using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// HttpTaskProgressSink pushes progress to the coordinator's
/// /api/v1/distribution/workers/.../progress endpoint. Fire-and-forget
/// design — must never throw on the caller's thread because it runs in
/// FFmpeg's progress callback. Throttling caps bandwidth at one POST per
/// 2 seconds per task.
/// </summary>
public class HttpTaskProgressSinkTests
{
    private static EncodingProgress Progress(double percent = 50.0) =>
        new(
            CorrelationId: "corr",
            PercentComplete: percent,
            Elapsed: TimeSpan.FromSeconds(10),
            EstimatedRemaining: TimeSpan.FromSeconds(10),
            CurrentFps: 30,
            CurrentSpeed: 1.5,
            CurrentStage: "encode",
            CurrentOperation: "av1_nvenc"
        );

    private sealed class TestHandler : HttpMessageHandler
    {
        public int RequestCount;
        public Exception? ThrowOnSend;
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref RequestCount);
            if (ThrowOnSend is not null)
                throw ThrowOnSend;
            return Task.FromResult(new HttpResponseMessage(StatusCode));
        }
    }

    private static (HttpTaskProgressSink Sink, TestHandler Handler) Build(
        string? coordinatorUrl = "https://coord.local"
    )
    {
        TestHandler handler = new();
        Mock<IHttpClientFactory> factory = new();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        EncoderOptions options = new() { CoordinatorUrl = coordinatorUrl, WorkerId = "w1" };

        return (
            new HttpTaskProgressSink(
                factory.Object,
                options,
                NullLogger<HttpTaskProgressSink>.Instance
            ),
            handler
        );
    }

    [Fact]
    public void Report_NoCoordinatorUrl_DoesNotPush()
    {
        // Standalone worker / coordinator-only host → nothing to push.
        // The sink must short-circuit before even resolving an HttpClient.
        (HttpTaskProgressSink sink, TestHandler handler) = Build(coordinatorUrl: null);

        sink.Report("task-1", Progress());

        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public void Report_WhitespaceCoordinatorUrl_DoesNotPush()
    {
        (HttpTaskProgressSink sink, TestHandler handler) = Build(coordinatorUrl: "   ");

        sink.Report("task-1", Progress());

        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task Report_WithCoordinator_SchedulesHttpPost()
    {
        (HttpTaskProgressSink sink, TestHandler handler) = Build();

        sink.Report("task-1", Progress());

        // Fire-and-forget; give the background task a moment to land.
        await WaitForCount(handler, 1);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Report_TwicePerTaskUnderInterval_OnlyFirstPushes()
    {
        // Throttle window is 2 seconds — the second report inside that
        // window MUST NOT trigger another POST.
        (HttpTaskProgressSink sink, TestHandler handler) = Build();

        sink.Report("task-1", Progress(percent: 10));
        sink.Report("task-1", Progress(percent: 11));

        await WaitForCount(handler, 1);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Report_DifferentTasks_BothPush_InsideWindow()
    {
        // The throttle is per-task, not global. Two distinct task IDs both
        // push immediately even when called back-to-back.
        (HttpTaskProgressSink sink, TestHandler handler) = Build();

        sink.Report("task-a", Progress());
        sink.Report("task-b", Progress());

        await WaitForCount(handler, 2);
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task Report_HttpHandlerThrows_DoesNotPropagate()
    {
        // ffmpeg's progress thread MUST NOT receive an exception from a
        // failed coordinator push. Best-effort.
        (HttpTaskProgressSink sink, TestHandler handler) = Build();
        handler.ThrowOnSend = new HttpRequestException("DNS unreachable");

        Action act = () => sink.Report("task-1", Progress());

        act.Should().NotThrow();
        await Task.Delay(50);
    }

    [Fact]
    public async Task Report_CoordinatorReturns500_DoesNotThrow()
    {
        // Non-success status MUST land as a debug log, not propagated.
        (HttpTaskProgressSink sink, TestHandler handler) = Build();
        handler.StatusCode = HttpStatusCode.InternalServerError;

        sink.Report("task-1", Progress());

        await WaitForCount(handler, 1);
        handler.RequestCount.Should().Be(1);
    }

    private static async Task WaitForCount(TestHandler handler, int expected)
    {
        // Fire-and-forget — poll briefly so the background task can run.
        for (int i = 0; i < 50 && handler.RequestCount < expected; i++)
            await Task.Delay(10);
    }
}
