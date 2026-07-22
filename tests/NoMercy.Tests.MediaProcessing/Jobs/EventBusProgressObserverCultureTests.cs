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
[Collection(name: "EventBusProvider")]
public class EventBusProgressObserverCultureTests
{
    private static object? GetField(object obj, string fieldName) =>
        obj.GetType().GetProperty(name: fieldName)?.GetValue(obj: obj);

    private static IEventBus? GetCurrentInstance() =>
        (IEventBus?)
            typeof(EventBusProvider)
                .GetField(name: "_instance", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(obj: null);

    private static void SetInstance(IEventBus? bus) =>
        typeof(EventBusProvider)
            .GetField(name: "_instance", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(obj: null, value: bus);

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void OnStageCompleted_Message_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
        IEventBus? previousBus = GetCurrentInstance();
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

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

            EventBusProgressObserver observer = new(jobId: 7, title: "Culture Test Movie");
            observer.OnStageCompleted(stageName: "VideoEncode", duration: TimeSpan.FromSeconds(value: 3.2));

            Assert.NotNull(@object: captured);
            string message = (string)GetField(obj: captured.ProgressData, fieldName: "message")!;
            Assert.Contains(expectedSubstring: "(3.2s)", actualString: message);
            Assert.DoesNotContain(expectedSubstring: ",", actualString: message);
        }
        finally
        {
            SetInstance(bus: previousBus);
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }
}
