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
/// <see cref="EventBusProgressObserver.OnStageCompleted"/> bakes an elapsed
/// duration into the "message" field of the SignalR dashboard payload. On a
/// comma-decimal server locale a bare ":F1" turns "(3.2s)" into "(3,2s)" in
/// that payload — this pins InvariantCulture on the formatter.
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
    public void OnStageCompleted_Message_StaysPeriodDecimalUnderCommaCulture(string culture)
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

            EventBusProgressObserver observer = new(7, "Culture Test Movie");
            observer.OnStageCompleted("VideoEncode", TimeSpan.FromSeconds(3.2));

            Assert.NotNull(captured);
            string message = (string)GetField(captured.ProgressData, "message")!;
            Assert.Contains("(3.2s)", message);
            Assert.DoesNotContain(",", message);
        }
        finally
        {
            SetInstance(previousBus);
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }
}
