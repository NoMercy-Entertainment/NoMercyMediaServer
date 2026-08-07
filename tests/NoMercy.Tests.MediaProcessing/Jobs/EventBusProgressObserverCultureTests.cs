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

using System.Globalization;
using System.Reflection;
using Moq;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// <see cref="EventBusProgressObserver.OnStageCompleted"/> used to bake an
/// elapsed duration into the "message" field of the SignalR dashboard
/// payload as a formatted string — "Completed: VideoEncode (3.2s)" — which on
/// a comma-decimal server locale turned "(3.2s)" into "(3,2s)" in that
/// payload, and grew the dashboard card's task-name text every heartbeat
/// instead of the card's own dedicated numbers row. The duration now travels
/// as a separate numeric "elapsed_seconds" field, which the JSON serializer
/// always writes invariant regardless of thread culture — this pins that.
/// </summary>
[Collection("EventBusProvider")]
public class EventBusProgressObserverCultureTests
{
    private static object? GetField(object obj, string fieldName) =>
        obj.GetType().GetProperty(fieldName)?.GetValue(obj);

    private static IEventBus? GetCurrentInstance() =>
        (IEventBus?)
            typeof(EventBusProvider)
                .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null);

    private static void SetInstance(IEventBus? bus) =>
        typeof(EventBusProvider)
            .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, bus);

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void OnStageCompleted_ElapsedSeconds_StaysPeriodDecimalUnderCommaCulture(
        string culture
    )
    {
        CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
        IEventBus? previousBus = GetCurrentInstance();
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            EncodingProgressBroadcastedEvent? captured = null;
            Mock<IEventBus> mockBus = new();
            mockBus
                .Setup(b =>
                    b.PublishAsync(
                        It.IsAny<EncodingProgressBroadcastedEvent>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<EncodingProgressBroadcastedEvent, CancellationToken>(
                    (e, _) => captured = e
                )
                .Returns(Task.CompletedTask);

            EventBusProvider.Configure(mockBus.Object);

            EventBusProgressObserver observer = new(jobId: 7, title: "Culture Test Movie");
            observer.OnStageCompleted("VideoEncode", TimeSpan.FromSeconds(3.2));

            Assert.NotNull(captured);
            string message = (string)GetField(captured.ProgressData, "message")!;
            Assert.Equal("Completed: VideoEncode", message);

            double? elapsedSeconds = (double?)GetField(captured.ProgressData, "elapsed_seconds");
            Assert.Equal(3.2, elapsedSeconds);
        }
        finally
        {
            SetInstance(previousBus);
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }
}
