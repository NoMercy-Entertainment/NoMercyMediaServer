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

[Collection("EventBusProvider")]
public class EventBusProgressObserverTests
{
    // Helpers that read fields off the anonymous ProgressData object.
    private static object? GetField(object obj, string fieldName) =>
        obj.GetType().GetProperty(fieldName)?.GetValue(obj);

    // Save and restore the static EventBusProvider._instance around a test body
    // so no state leaks regardless of ordering or test failure.
    private static IEventBus? GetCurrentInstance() =>
        (IEventBus?)
            typeof(EventBusProvider)
                .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null);

    private static void SetInstance(IEventBus? bus) =>
        typeof(EventBusProvider)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, bus);

    [Fact]
    public void OnProgress_WhenEventBusNotConfigured_DoesNotThrow()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            SetInstance(null);

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
        finally
        {
            SetInstance(previous);
        }
    }

    [Fact]
    public void OnStageStarted_WhenEventBusConfigured_PublishesStageChangedEvent()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncoderProgressBroadcastEvent? captured = null;
            Mock<IEventBus> mockBus = new();
            mockBus
                .Setup(b =>
                    b.PublishAsync(
                        It.IsAny<EncoderProgressBroadcastEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncoderProgressBroadcastEvent, CancellationToken>((e, _) => captured = e)
                .Returns(Task.CompletedTask);

            EventBusProvider.Configure(mockBus.Object);

            EventBusProgressObserver observer = new(jobId: 42, title: "Action Movie");
            observer.OnStageStarted("VideoEncode");

            mockBus.Verify(
                b =>
                    b.PublishAsync(
                        It.IsAny<EncoderProgressBroadcastEvent>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            Assert.NotNull(captured);
            object progressData = captured.ProgressData;
            Assert.Equal(42, (int)GetField(progressData, "id")!);
            Assert.Equal("encoding", (string)GetField(progressData, "status")!);
            Assert.Equal("Action Movie", (string)GetField(progressData, "title")!);
            Assert.Equal("Stage: VideoEncode", (string)GetField(progressData, "message")!);
        }
        finally
        {
            SetInstance(previous);
        }
    }

    [Fact]
    public void OnError_WhenEventBusConfigured_PublishesStageChangedEventWithFailedStatus()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncoderProgressBroadcastEvent? captured = null;
            Mock<IEventBus> mockBus = new();
            mockBus
                .Setup(b =>
                    b.PublishAsync(
                        It.IsAny<EncoderProgressBroadcastEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncoderProgressBroadcastEvent, CancellationToken>((e, _) => captured = e)
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
                        It.IsAny<EncoderProgressBroadcastEvent>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
            Assert.NotNull(captured);
            object progressData = captured.ProgressData;
            Assert.Equal(7, (int)GetField(progressData, "id")!);
            Assert.Equal("failed", (string)GetField(progressData, "status")!);
            Assert.Equal("Documentary", (string)GetField(progressData, "title")!);
            Assert.Equal("FFmpeg crashed", (string)GetField(progressData, "message")!);
        }
        finally
        {
            SetInstance(previous);
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
