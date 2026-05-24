using System.Reflection;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.Encoder.Integration;

/// <summary>
/// Tests for EventBusProgressObserver — verifies that OnProgress publishes
/// an EncoderProgressBroadcastEvent to the configured event bus, and that
/// it is silent when no bus is configured.
/// </summary>
[Collection("EventBusProgressObserver")]
public sealed class EventBusProgressObserverTests : IDisposable
{
    // Reset the static EventBusProvider between tests via reflection.
    private static void ResetEventBusProvider()
    {
        FieldInfo? field = typeof(EventBusProvider).GetField(
            "_instance",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        field?.SetValue(null, null);
    }

    public EventBusProgressObserverTests()
    {
        ResetEventBusProvider();
    }

    public void Dispose()
    {
        ResetEventBusProvider();
    }

    private static EncodingProgress MakeProgress(double percent = 42.5, int pid = 1234) =>
        new(
            CorrelationId: "test-corr-1",
            PercentComplete: percent,
            Elapsed: TimeSpan.FromSeconds(10),
            EstimatedRemaining: TimeSpan.FromSeconds(14),
            CurrentFps: 24.0,
            CurrentSpeed: 1.5,
            CurrentStage: "video",
            CurrentOperation: "encoding",
            BitrateKbps: 4000,
            Bitrate: "4000kb/s",
            ProcessId: pid,
            CurrentTimeSeconds: 10.0,
            DurationSeconds: 120.0
        );

    [Fact]
    public void OnProgress_WhenEventBusConfigured_PublishesEvent()
    {
        // Arrange — wire up an InMemoryEventBus and capture published events.
        InMemoryEventBus bus = new();
        List<EncoderProgressBroadcastEvent> captured = [];

        bus.Subscribe<EncoderProgressBroadcastEvent>(
            (evt, _) =>
            {
                captured.Add(evt);
                return Task.CompletedTask;
            }
        );

        EventBusProvider.Configure(bus);

        EventBusProgressObserver observer = new(
            jobId: 99,
            title: "Test Movie",
            baseFolder: "/media/test",
            sharePath: "/share/test",
            videoStreams: ["1080p"],
            audioStreams: ["AAC"],
            subtitleStreams: ["EN"],
            hasGpu: true,
            isHdr: false
        );

        EncodingProgress progress = MakeProgress(percent: 42.5, pid: 1234);

        // Act
        observer.OnProgress(progress);

        // Assert
        captured.Should().HaveCount(1);
        EncoderProgressBroadcastEvent published = captured[0];
        published.Should().NotBeNull();
        published.Source.Should().Be("Encoder");
        published.ProgressData.Should().NotBeNull();

        // Verify key fields via dynamic reflection on the anonymous ProgressData object.
        object data = published.ProgressData;
        Type dataType = data.GetType();

        int id = (int)(dataType.GetProperty("id")?.GetValue(data) ?? -1);
        id.Should().Be(99);

        string title = (string)(dataType.GetProperty("title")?.GetValue(data) ?? "");
        title.Should().Be("Test Movie");

        double pct = (double)(dataType.GetProperty("progress")?.GetValue(data) ?? -1.0);
        pct.Should().BeApproximately(42.5, 0.001);

        int processId = (int)(dataType.GetProperty("process_id")?.GetValue(data) ?? -1);
        processId.Should().Be(1234);

        bool hasGpu = (bool)(dataType.GetProperty("has_gpu")?.GetValue(data) ?? false);
        hasGpu.Should().BeTrue();
    }

    [Fact]
    public void OnProgress_WhenEventBusNotConfigured_DoesNotThrow()
    {
        // Arrange — EventBusProvider is not configured (reset in constructor).
        EventBusProvider.IsConfigured.Should().BeFalse();

        EventBusProgressObserver observer = new(jobId: 1, title: "Unconfigured Test");

        EncodingProgress progress = MakeProgress();

        // Act + Assert — must not throw.
        Action act = () => observer.OnProgress(progress);
        act.Should().NotThrow();
    }
}
