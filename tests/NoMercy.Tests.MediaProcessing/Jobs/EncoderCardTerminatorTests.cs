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
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// Guards the terminal-event contract behind the "completed tasks linger"
/// fix: a stranded encode card is added by an in-flight encoder-progress status
/// and removed only by a non-inflight status on that channel or a matching
/// <see cref="EncodingFailedEvent"/>. Every abnormal coordinator exit routes
/// through <see cref="EncoderCardTerminator"/>, so it must emit a status the
/// dashboard treats as terminal — not one of running / paused / encoding, which
/// the client keeps showing.
/// </summary>
[Collection("EventBusProvider")]
public class EncoderCardTerminatorTests
{
    // The statuses the web dashboard treats as "still in flight" and keeps a
    // card visible for (ServerTasksCard.handleProgress). A terminal event MUST
    // NOT carry one of these or the card never clears.
    private static readonly string[] InFlightStatuses = ["running", "paused", "encoding"];

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
    public async Task PublishFailedAsync_EmitsTerminalStatus_AndMatchingFailedEvent()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncodingStageChangedEvent? stage = null;
            EncodingFailedEvent? failed = null;

            Mock<IEventBus> bus = new();
            bus.Setup(b =>
                    b.PublishAsync(
                        It.IsAny<EncodingStageChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncodingStageChangedEvent, CancellationToken>((e, _) => stage = e)
                .Returns(Task.CompletedTask);
            bus.Setup(b =>
                    b.PublishAsync(It.IsAny<EncodingFailedEvent>(), It.IsAny<CancellationToken>())
                )
                .Callback<EncodingFailedEvent, CancellationToken>((e, _) => failed = e)
                .Returns(Task.CompletedTask);

            EventBusProvider.Configure(bus.Object);

            await EncoderCardTerminator.PublishFailedAsync(
                jobId: 88061,
                title: "Hensuki",
                inputPath: "/media/anime/hensuki.mkv",
                message: "finalize-only pass failed with no details",
                exceptionType: "FinalizeFailed"
            );

            // The stage event is what the card list itself listens on; its status
            // must fall outside the in-flight set so the client drops the card.
            stage.Should().NotBeNull();
            stage!.JobId.Should().Be(88061);
            stage.Title.Should().Be("Hensuki");
            InFlightStatuses.Should().NotContain(stage.Status);
            stage.Status.Should().Be("failed");

            // The dedicated failed event is the second removal path, keyed by the
            // same id so it clears the card even if the stage channel is missed.
            failed.Should().NotBeNull();
            failed!.JobId.Should().Be(88061);
            failed.ErrorMessage.Should().Be("finalize-only pass failed with no details");
            failed.ExceptionType.Should().Be("FinalizeFailed");
        }
        finally
        {
            SetInstance(previous);
        }
    }

    /// <summary>
    /// The two artwork parameters are optional so every pre-metadata failure
    /// site (which never resolved a movie/episode) keeps compiling unchanged;
    /// when a caller does have them, they must reach the failed event verbatim
    /// so PushNotificationEventHandler can build an Image/Icon from them.
    /// </summary>
    [Fact]
    public async Task PublishFailedAsync_WithArtwork_CarriesPosterAndBackdropOnTheFailedEvent()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            EncodingFailedEvent? failed = null;

            Mock<IEventBus> bus = new();
            bus.Setup(b =>
                    b.PublishAsync(
                        It.IsAny<EncodingStageChangedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);
            bus.Setup(b =>
                    b.PublishAsync(It.IsAny<EncodingFailedEvent>(), It.IsAny<CancellationToken>())
                )
                .Callback<EncodingFailedEvent, CancellationToken>((e, _) => failed = e)
                .Returns(Task.CompletedTask);

            EventBusProvider.Configure(bus.Object);

            await EncoderCardTerminator.PublishFailedAsync(
                jobId: 88062,
                title: "Idiocracy",
                inputPath: "/media/movies/idiocracy.mkv",
                message: "finalize-only pass failed with no details",
                exceptionType: "FinalizeFailed",
                posterPath: "/poster123.jpg",
                backdropPath: "/backdrop123.jpg"
            );

            failed.Should().NotBeNull();
            failed!.PosterPath.Should().Be("/poster123.jpg");
            failed.BackdropPath.Should().Be("/backdrop123.jpg");
        }
        finally
        {
            SetInstance(previous);
        }
    }

    [Fact]
    public async Task PublishFailedAsync_WhenEventBusNotConfigured_DoesNotThrow()
    {
        IEventBus? previous = GetCurrentInstance();
        try
        {
            SetInstance(null);

            Func<Task> act = () =>
                EncoderCardTerminator.PublishFailedAsync(
                    jobId: 1,
                    title: "Untitled",
                    inputPath: "/media/x.mkv",
                    message: "boom",
                    exceptionType: "InvalidOperationException"
                );

            await act.Should().NotThrowAsync();
        }
        finally
        {
            SetInstance(previous);
        }
    }
}
