using System.Reflection;
using Moq;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.MediaProcessing.Jobs;

[Collection("EventBusProvider")]
public class EventBusProgressObserverTests
{
    [Fact]
    public void OnProgress_WhenEventBusNotConfigured_DoesNotThrow()
    {
        // Reach an unconfigured state by reflection — EventBusProvider has no Reset(),
        // so we clear the backing field directly.
        typeof(EventBusProvider)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);

        EventBusProgressObserver observer = new(jobId: 1, title: "Test Movie");

        EncodingProgress progress = new(
            CorrelationId: "test-id",
            PercentComplete: 50.0,
            Elapsed: TimeSpan.FromSeconds(10),
            EstimatedRemaining: TimeSpan.FromSeconds(10),
            CurrentFps: 30.0,
            CurrentSpeed: 1.0,
            CurrentStage: "video",
            CurrentOperation: "encode"
        );

        Exception? ex = Record.Exception(() => observer.OnProgress(progress));
        Assert.Null(ex);
    }

    [Fact]
    public void OnStageStarted_WhenEventBusConfigured_PublishesStageChangedEvent()
    {
        EncodingStageChangedEvent? captured = null;
        Mock<IEventBus> mockBus = new();
        mockBus
            .Setup(b =>
                b.PublishAsync(It.IsAny<EncodingStageChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<EncodingStageChangedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(mockBus.Object);

        EventBusProgressObserver observer = new(jobId: 42, title: "Action Movie");
        observer.OnStageStarted("VideoEncode");

        mockBus.Verify(
            b =>
                b.PublishAsync(
                    It.IsAny<EncodingStageChangedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        Assert.NotNull(captured);
        Assert.Equal(42, (int)captured.JobId);
        Assert.Equal("encoding", captured.Status);
        Assert.Equal("Action Movie", captured.Title);
        Assert.Equal("Stage: VideoEncode", captured.Message);

        // Reset
        EventBusProvider.Configure(mockBus.Object);
    }

    [Fact]
    public void OnError_WhenEventBusConfigured_PublishesStageChangedEventWithFailedStatus()
    {
        EncodingStageChangedEvent? captured = null;
        Mock<IEventBus> mockBus = new();
        mockBus
            .Setup(b =>
                b.PublishAsync(It.IsAny<EncodingStageChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<EncodingStageChangedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(mockBus.Object);

        EventBusProgressObserver observer = new(jobId: 7, title: "Documentary");
        EncodingError error = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: "FFmpeg crashed",
            FfmpegStderr: null,
            StageName: "VideoEncode",
            Recoverable: false
        );
        observer.OnError(error);

        mockBus.Verify(
            b =>
                b.PublishAsync(
                    It.IsAny<EncodingStageChangedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        Assert.NotNull(captured);
        Assert.Equal(7, (int)captured.JobId);
        Assert.Equal("failed", captured.Status);
        Assert.Equal("Documentary", captured.Title);
        Assert.Equal("FFmpeg crashed", captured.Message);

        // Reset
        EventBusProvider.Configure(mockBus.Object);
    }

    [Fact]
    public void OnProgress_WithRegistry_RegistersFirstSeenPid()
    {
        EncoderProcessRegistry registry = new();
        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);

        EncodingProgress progress = new(
            CorrelationId: "c",
            PercentComplete: 10.0,
            Elapsed: TimeSpan.FromSeconds(1),
            EstimatedRemaining: null,
            CurrentFps: 24.0,
            CurrentSpeed: 1.0,
            CurrentStage: "video",
            CurrentOperation: null,
            ProcessId: 9876
        );

        observer.OnProgress(progress);

        Assert.Contains(9876, registry.GetProcessIds(42));
    }

    [Fact]
    public void OnProgress_IdempotentForSamePid_DoesNotGrowRegistry()
    {
        EncoderProcessRegistry registry = new();
        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);

        EncodingProgress progress = new(
            CorrelationId: "c",
            PercentComplete: 10.0,
            Elapsed: TimeSpan.FromSeconds(1),
            EstimatedRemaining: null,
            CurrentFps: 24.0,
            CurrentSpeed: 1.0,
            CurrentStage: "video",
            CurrentOperation: null,
            ProcessId: 9876
        );

        observer.OnProgress(progress);
        observer.OnProgress(progress);
        observer.OnProgress(progress);

        Assert.Single(registry.GetProcessIds(42));
    }

    [Fact]
    public void OnCompleted_UnregistersJob()
    {
        EncoderProcessRegistry registry = new();
        registry.Register(42, 9876);

        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);
        observer.OnCompleted();

        Assert.Empty(registry.GetProcessIds(42));
    }

    [Fact]
    public void OnError_UnregistersJob()
    {
        EncoderProcessRegistry registry = new();
        registry.Register(42, 9876);

        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);
        EncodingError error = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: "boom",
            FfmpegStderr: null,
            StageName: "Execute",
            Recoverable: false
        );
        observer.OnError(error);

        Assert.Empty(registry.GetProcessIds(42));
    }
}
