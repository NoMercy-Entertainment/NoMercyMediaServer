using FluentAssertions;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using Xunit;

namespace NoMercy.Tests.Events;

public class EncodingPipelineEventTests
{
    [Fact]
    public async Task EncodingPipeline_PublishesStartedProgressCompleted_InOrder()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<EncodingStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingProgressEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        await bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 42,
            InputPath = "/input/video.mkv",
            OutputPath = "/output/video/",
            ProfileName = "HLS-1080p"
        });

        await bus.PublishAsync(new EncodingProgressEvent
        {
            JobId = 42,
            Percentage = 25.0,
            Elapsed = TimeSpan.FromMinutes(5),
            Estimated = TimeSpan.FromMinutes(15)
        });

        await bus.PublishAsync(new EncodingProgressEvent
        {
            JobId = 42,
            Percentage = 75.0,
            Elapsed = TimeSpan.FromMinutes(15),
            Estimated = TimeSpan.FromMinutes(5)
        });

        await bus.PublishAsync(new EncodingCompletedEvent
        {
            JobId = 42,
            OutputPath = "/output/video/",
            Duration = TimeSpan.FromMinutes(20)
        });

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<EncodingStartedEvent>();
        received[1].Should().BeOfType<EncodingProgressEvent>();
        received[2].Should().BeOfType<EncodingProgressEvent>();
        received[3].Should().BeOfType<EncodingCompletedEvent>();

        EncodingStartedEvent started = (EncodingStartedEvent)received[0];
        started.JobId.Should().Be(42);
        started.InputPath.Should().Be("/input/video.mkv");
        started.ProfileName.Should().Be("HLS-1080p");

        EncodingProgressEvent progress1 = (EncodingProgressEvent)received[1];
        progress1.Percentage.Should().Be(25.0);
        progress1.Estimated.Should().Be(TimeSpan.FromMinutes(15));

        EncodingProgressEvent progress2 = (EncodingProgressEvent)received[2];
        progress2.Percentage.Should().Be(75.0);

        EncodingCompletedEvent completed = (EncodingCompletedEvent)received[3];
        completed.Duration.Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task EncodingPipeline_PublishesStartedThenFailed_OnError()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<EncodingStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingFailedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        await bus.PublishAsync(new EncodingStartedEvent
        {
            JobId = 99,
            InputPath = "/input/corrupt.mkv",
            OutputPath = "/output/corrupt/",
            ProfileName = "HLS-720p"
        });

        await bus.PublishAsync(new EncodingFailedEvent
        {
            JobId = 99,
            InputPath = "/input/corrupt.mkv",
            ErrorMessage = "FFmpeg exited with code 1",
            ExceptionType = "InvalidOperationException"
        });

        received.Should().HaveCount(2);
        received[0].Should().BeOfType<EncodingStartedEvent>();
        received[1].Should().BeOfType<EncodingFailedEvent>();

        EncodingFailedEvent failed = (EncodingFailedEvent)received[1];
        failed.JobId.Should().Be(99);
        failed.ErrorMessage.Should().Be("FFmpeg exited with code 1");
        failed.ExceptionType.Should().Be("InvalidOperationException");
    }

    [Fact]
    public async Task EncodingProgressEvent_WorksWithGuidHashCodeAsJobId()
    {
        InMemoryEventBus bus = new();
        EncodingProgressEvent? receivedEvent = null;

        bus.Subscribe<EncodingProgressEvent>((evt, _) =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        });

        Guid trackId = Guid.NewGuid();
        int jobId = trackId.GetHashCode();

        await bus.PublishAsync(new EncodingProgressEvent
        {
            JobId = jobId,
            Percentage = 50.0,
            Elapsed = TimeSpan.FromMinutes(3)
        });

        receivedEvent.Should().NotBeNull();
        receivedEvent!.JobId.Should().Be(jobId);
        receivedEvent.Percentage.Should().Be(50.0);
    }

    [Fact]
    public async Task EventBusProvider_CanPublishEncodingEvents_WhenConfigured()
    {
        InMemoryEventBus bus = new();
        EventBusProvider.Configure(bus);

        List<IEvent> received = [];
        bus.Subscribe<EncodingStartedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<EncodingCompletedEvent>((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        EventBusProvider.IsConfigured.Should().BeTrue();

        await EventBusProvider.Current.PublishAsync(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/test.mkv",
            OutputPath = "/out/",
            ProfileName = "Default"
        });

        await EventBusProvider.Current.PublishAsync(new EncodingCompletedEvent
        {
            JobId = 1,
            OutputPath = "/out/",
            Duration = TimeSpan.FromSeconds(30)
        });

        received.Should().HaveCount(2);
        received[0].Should().BeOfType<EncodingStartedEvent>();
        received[1].Should().BeOfType<EncodingCompletedEvent>();
    }

    [Fact]
    public async Task EncodingEvents_HaveUniqueEventIds()
    {
        EncodingStartedEvent started = new()
        {
            JobId = 1,
            InputPath = "/test",
            OutputPath = "/out",
            ProfileName = "p"
        };

        EncodingProgressEvent progress = new()
        {
            JobId = 1,
            Percentage = 50.0,
            Elapsed = TimeSpan.FromMinutes(1)
        };

        EncodingCompletedEvent completed = new()
        {
            JobId = 1,
            OutputPath = "/out",
            Duration = TimeSpan.FromMinutes(2)
        };

        EncodingFailedEvent failed = new()
        {
            JobId = 1,
            InputPath = "/test",
            ErrorMessage = "error"
        };

        Guid[] eventIds = [started.EventId, progress.EventId, completed.EventId, failed.EventId];
        eventIds.Should().OnlyHaveUniqueItems();
        eventIds.Should().NotContain(Guid.Empty);
    }

    [Fact]
    public void EncodingEvents_AllHaveEncoderSource()
    {
        IEvent[] events =
        [
            new EncodingStartedEvent { JobId = 1, InputPath = "/i", OutputPath = "/o", ProfileName = "p" },
            new EncodingProgressEvent { JobId = 1, Percentage = 0, Elapsed = TimeSpan.Zero },
            new EncodingCompletedEvent { JobId = 1, OutputPath = "/o", Duration = TimeSpan.Zero },
            new EncodingFailedEvent { JobId = 1, InputPath = "/i", ErrorMessage = "e" }
        ];

        foreach (IEvent evt in events)
        {
            evt.Source.Should().Be("Encoder");
        }
    }
}
