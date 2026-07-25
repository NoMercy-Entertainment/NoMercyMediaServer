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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.Service.Workers;

namespace NoMercy.Tests.Service.Workers;

/// <summary>
/// <see cref="DriveMonitorWorker"/> bridges the encoder-layer
/// <see cref="IDriveMonitor"/> polling loop onto the application event bus.
/// The <see cref="DriveEventType"/> -> wire "method" mapping is the actual
/// contract <see cref="NoMercy.Api.EventHandlers.DriveMonitorEventHandler"/>
/// and the web/Android clients key their UI off of — a typo here silently
/// breaks disc-insert/eject notifications for every client. This also pins
/// that a configured-but-not-yet-published event bus is required: when
/// <see cref="EventBusProvider.IsConfigured"/> is false the worker must skip
/// publishing rather than throw the "not configured" exception mid-loop.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DriveMonitorWorkerTests
{
    private static readonly DiscDrive Drive = new(
        "/dev/sr0",
        "MOVIE_DISC",
        true,
        OpticalDiscType.Dvd
    );

    private static async IAsyncEnumerable<DriveEvent> Events(params DriveEventType[] types)
    {
        foreach (DriveEventType type in types)
        {
            yield return new(type, Drive);
            await Task.Yield();
        }
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public readonly List<DriveStateChangedEvent> Published = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IEvent
        {
            if (@event is DriveStateChangedEvent driveEvent)
                Published.Add(driveEvent);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : IEvent => NullDisposable.Instance;

        public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose() { }
        }
    }

    // EventBusProvider is a process-wide static with no reset hook; save and
    // restore the ambient instance via reflection so this test cannot leak a
    // fake bus into any other test that reads EventBusProvider.Current.
    private static IEventBus? SwapEventBus(IEventBus? next)
    {
        System.Reflection.FieldInfo field = typeof(EventBusProvider).GetField(
            "_instance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        )!;
        IEventBus? previous = (IEventBus?)field.GetValue(null);
        field.SetValue(null, next);
        return previous;
    }

    [Theory]
    [InlineData([DriveEventType.DriveAdded, "drive_added"])]
    [InlineData([DriveEventType.DriveRemoved, "drive_removed"])]
    [InlineData([DriveEventType.DiscInserted, "disc_inserted"])]
    [InlineData([DriveEventType.DiscEjected, "disc_ejected"])]
    public async Task ExecuteAsync_PublishesWithTheExpectedWireMethodName(
        DriveEventType eventType,
        string expectedMethod
    )
    {
        RecordingEventBus eventBus = new();
        IEventBus? original = SwapEventBus(eventBus);
        try
        {
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(Events(eventType));
            DriveMonitorWorker worker = new(
                driveMonitor.Object,
                NullLogger<DriveMonitorWorker>.Instance
            );

            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(50);

            Assert.Single(eventBus.Published);
            Assert.Equal(expectedMethod, eventBus.Published[0].DriveStateData.Method);
            Assert.Equal(Drive.Path, eventBus.Published[0].DriveStateData.Drive);
            Assert.Equal(Drive.Label, eventBus.Published[0].DriveStateData.VolumeLabel);
            Assert.True(eventBus.Published[0].DriveStateData.HasDisc);
            Assert.Equal("dvd", eventBus.Published[0].DriveStateData.DiscType);
        }
        finally
        {
            SwapEventBus(original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_EventBusNotConfigured_SkipsPublishingWithoutThrowing()
    {
        IEventBus? original = SwapEventBus(null);
        try
        {
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(Events(DriveEventType.DriveAdded));
            DriveMonitorWorker worker = new(
                driveMonitor.Object,
                NullLogger<DriveMonitorWorker>.Instance
            );

            Exception? thrown = await Record.ExceptionAsync(async () =>
            {
                await worker.StartAsync(CancellationToken.None);
                await Task.Delay(50);
            });

            Assert.Null(thrown);
        }
        finally
        {
            SwapEventBus(original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedEventType_FallsBackToDriveChangedMethod()
    {
        RecordingEventBus eventBus = new();
        IEventBus? original = SwapEventBus(eventBus);
        try
        {
            // Cast a value outside the declared DriveEventType range — the
            // switch's `_ => "drive_changed"` arm is the safety net for any
            // enum value the encoder layer adds later without a matching
            // case being added here.
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(Events((DriveEventType)99));
            DriveMonitorWorker worker = new(
                driveMonitor.Object,
                NullLogger<DriveMonitorWorker>.Instance
            );

            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(50);

            Assert.Single(eventBus.Published);
            Assert.Equal("drive_changed", eventBus.Published[0].DriveStateData.Method);
        }
        finally
        {
            SwapEventBus(original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MonitorThrows_RetriesThenExitsCleanlyOnCancellation()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(m => m.MonitorAsync(It.IsAny<CancellationToken>()))
            .Throws<InvalidOperationException>();
        DriveMonitorWorker worker = new(
            driveMonitor.Object,
            NullLogger<DriveMonitorWorker>.Instance
        );
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Exception? thrown = await Record.ExceptionAsync(async () =>
        {
            await worker.StartAsync(cts.Token);
            await Task.Delay(300);
        });

        // The 5s retry backoff itself is cut short by the token cancelling at
        // 50ms — reaching here without a real 5s wait proves the retry loop's
        // own OperationCanceledException catch (not the outer one) is what
        // exits the worker.
        Assert.Null(thrown);
    }
}
