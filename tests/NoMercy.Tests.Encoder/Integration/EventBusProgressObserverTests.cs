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

using System.Reflection;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.Encoder.Integration;

/// <summary>
/// Tests for EventBusProgressObserver — verifies that OnProgress publishes
/// an EncodingProgressBroadcastedEvent to the configured event bus, and that
/// it is silent when no bus is configured.
/// </summary>
[Collection(name: "EventBusProgressObserver")]
public sealed class EventBusProgressObserverTests : IDisposable
{
    // Reset the static EventBusProvider between tests via reflection.
    private static void ResetEventBusProvider()
    {
        FieldInfo? field = typeof(EventBusProvider).GetField(
            name: "_instance",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
        );
        field?.SetValue(obj: null, value: null);
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
            Elapsed: TimeSpan.FromSeconds(seconds: 10),
            EstimatedRemaining: TimeSpan.FromSeconds(seconds: 14),
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
        List<EncodingProgressBroadcastedEvent> captured = [];

        bus.Subscribe<EncodingProgressBroadcastedEvent>(
            handler: (evt, _) =>
            {
                captured.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        EventBusProvider.Configure(eventBus: bus);

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
        observer.OnProgress(progress: progress);

        // Assert
        captured.Should().HaveCount(expected: 1);
        EncodingProgressBroadcastedEvent published = captured[index: 0];
        published.Should().NotBeNull();
        published.Source.Should().Be(expected: "Encoder");
        published.ProgressData.Should().NotBeNull();

        // Verify key fields via dynamic reflection on the anonymous ProgressData object.
        object data = published.ProgressData;
        Type dataType = data.GetType();

        int id = (int)(dataType.GetProperty(name: "id")?.GetValue(obj: data) ?? -1);
        id.Should().Be(expected: 99);

        string title = (string)(dataType.GetProperty(name: "title")?.GetValue(obj: data) ?? "");
        title.Should().Be(expected: "Test Movie");

        double pct = (double)(dataType.GetProperty(name: "progress")?.GetValue(obj: data) ?? -1.0);
        pct.Should().BeApproximately(expectedValue: 42.5, precision: 0.001);

        int processId = (int)(dataType.GetProperty(name: "process_id")?.GetValue(obj: data) ?? -1);
        processId.Should().Be(expected: 1234);

        bool hasGpu = (bool)(dataType.GetProperty(name: "has_gpu")?.GetValue(obj: data) ?? false);
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
        Action act = () => observer.OnProgress(progress: progress);
        act.Should().NotThrow();
    }
}
