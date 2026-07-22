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
using Moq;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.MediaProcessing.Jobs;

[Collection(name: "EventBusProvider")]
public class EventBusProgressObserverTests
{
    // Helpers that read fields off the anonymous ProgressData object.
    private static object? GetField(object obj, string fieldName) =>
        obj.GetType().GetProperty(name: fieldName)?.GetValue(obj: obj);

    // Save and restore the static EventBusProvider._instance around a test body
    // so no state leaks regardless of ordering or test failure.
    private static IEventBus? GetCurrentInstance() =>
        (IEventBus?)
            typeof(EventBusProvider)
                .GetField(name: "_instance", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(obj: null);

    private static void SetInstance(IEventBus? bus) =>
        typeof(EventBusProvider)
            .GetField(name: "_instance", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(obj: null, value: bus);

    [Fact]
    public void OnProgress_WhenEventBusNotConfigured_DoesNotThrow()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            SetInstance(bus: null);

            EventBusProgressObserver observer = new(jobId: 1, title: "Test Movie");

            EncodingProgress progress = new(
                CorrelationId: "test-id",
                PercentComplete: 50.0,
                Elapsed: TimeSpan.FromSeconds(seconds: 10),
                EstimatedRemaining: TimeSpan.FromSeconds(seconds: 10),
                CurrentFps: 30.0,
                CurrentSpeed: 1.0,
                CurrentStage: "video",
                CurrentOperation: "encode"
            );

            Exception? ex = Record.Exception(testCode: () => observer.OnProgress(progress: progress));
            Assert.Null(@object: ex);
        }
        finally
        {
            SetInstance(bus: previous);
        }
    }

    [Fact]
    public void OnStageStarted_WhenEventBusConfigured_PublishesStageChangedEvent()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncodingProgressBroadcastedEvent? captured = null;
            Mock<IEventBus> mockBus = new();
            mockBus
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<EncodingProgressBroadcastedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncodingProgressBroadcastedEvent, CancellationToken>(
                    action: (e, _) => captured = e
                )
                .Returns(value: Task.CompletedTask);

            EventBusProvider.Configure(eventBus: mockBus.Object);

            EventBusProgressObserver observer = new(jobId: 42, title: "Action Movie");
            observer.OnStageStarted(stageName: "VideoEncode");

            mockBus.Verify(
                expression: b =>
                    b.PublishAsync(
                        It.IsAny<EncodingProgressBroadcastedEvent>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Once
            );
            Assert.NotNull(@object: captured);
            object progressData = captured.ProgressData;
            Assert.Equal(expected: 42, actual: (int)GetField(obj: progressData, fieldName: "id")!);
            Assert.Equal(expected: "encoding", actual: (string)GetField(obj: progressData, fieldName: "status")!);
            Assert.Equal(expected: "Action Movie", actual: (string)GetField(obj: progressData, fieldName: "title")!);
            Assert.Equal(expected: "Stage: VideoEncode", actual: (string)GetField(obj: progressData, fieldName: "message")!);
        }
        finally
        {
            SetInstance(bus: previous);
        }
    }

    [Fact]
    public void OnError_WhenEventBusConfigured_PublishesStageChangedEventWithFailedStatus()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncodingProgressBroadcastedEvent? captured = null;
            Mock<IEventBus> mockBus = new();
            mockBus
                .Setup(expression: b =>
                    b.PublishAsync(
                        It.IsAny<EncodingProgressBroadcastedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncodingProgressBroadcastedEvent, CancellationToken>(
                    action: (e, _) => captured = e
                )
                .Returns(value: Task.CompletedTask);

            EventBusProvider.Configure(eventBus: mockBus.Object);

            EventBusProgressObserver observer = new(jobId: 7, title: "Documentary");
            EncodingError error = new(
                Kind: EncodingErrorKind.ProcessCrashed,
                Message: "FFmpeg crashed",
                FfmpegStderr: null,
                StageName: "VideoEncode",
                Recoverable: false
            );
            observer.OnError(error: error);

            mockBus.Verify(
                expression: b =>
                    b.PublishAsync(
                        It.IsAny<EncodingProgressBroadcastedEvent>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Once
            );
            Assert.NotNull(@object: captured);
            object progressData = captured.ProgressData;
            Assert.Equal(expected: 7, actual: (int)GetField(obj: progressData, fieldName: "id")!);
            Assert.Equal(expected: "failed", actual: (string)GetField(obj: progressData, fieldName: "status")!);
            Assert.Equal(expected: "Documentary", actual: (string)GetField(obj: progressData, fieldName: "title")!);
            Assert.Equal(expected: "FFmpeg crashed", actual: (string)GetField(obj: progressData, fieldName: "message")!);
        }
        finally
        {
            SetInstance(bus: previous);
        }
    }

    [Fact]
    public void OnProgress_WithRegistry_RegistersFirstSeenPid()
    {
        EncoderProcessRegistry registry = new();
        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);

        EncodingProgress progress = new(
            CorrelationId: "c",
            PercentComplete: 10.0,
            Elapsed: TimeSpan.FromSeconds(seconds: 1),
            EstimatedRemaining: null,
            CurrentFps: 24.0,
            CurrentSpeed: 1.0,
            CurrentStage: "video",
            CurrentOperation: null,
            ProcessId: 9876
        );

        observer.OnProgress(progress: progress);

        Assert.Contains(expected: 9876, collection: registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void OnProgress_IdempotentForSamePid_DoesNotGrowRegistry()
    {
        EncoderProcessRegistry registry = new();
        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);

        EncodingProgress progress = new(
            CorrelationId: "c",
            PercentComplete: 10.0,
            Elapsed: TimeSpan.FromSeconds(seconds: 1),
            EstimatedRemaining: null,
            CurrentFps: 24.0,
            CurrentSpeed: 1.0,
            CurrentStage: "video",
            CurrentOperation: null,
            ProcessId: 9876
        );

        observer.OnProgress(progress: progress);
        observer.OnProgress(progress: progress);
        observer.OnProgress(progress: progress);

        Assert.Single(collection: registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void OnCompleted_UnregistersJob()
    {
        EncoderProcessRegistry registry = new();
        registry.Register(jobId: 42, processId: 9876);

        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);
        observer.OnCompleted();

        Assert.Empty(collection: registry.GetProcessIds(jobId: 42));
    }

    [Fact]
    public void OnError_UnregistersJob()
    {
        EncoderProcessRegistry registry = new();
        registry.Register(jobId: 42, processId: 9876);

        EventBusProgressObserver observer = new(jobId: 42, title: "Test", registry: registry);
        EncodingError error = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: "boom",
            FfmpegStderr: null,
            StageName: "Execute",
            Recoverable: false
        );
        observer.OnError(error: error);

        Assert.Empty(collection: registry.GetProcessIds(jobId: 42));
    }
}
