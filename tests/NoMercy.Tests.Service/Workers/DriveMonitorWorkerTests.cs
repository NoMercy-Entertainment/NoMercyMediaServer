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
using Xunit;

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
[Trait(name: "Category", value: "Unit")]
public sealed class DriveMonitorWorkerTests
{
    private static readonly DiscDrive Drive = new(
        Path: "/dev/sr0",
        Label: "MOVIE_DISC",
        HasDisc: true,
        DiscType: OpticalDiscType.Dvd
    );

    private static async IAsyncEnumerable<DriveEvent> Events(params DriveEventType[] types)
    {
        foreach (DriveEventType type in types)
        {
            yield return new(Type: type, Drive: Drive);
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
                Published.Add(item: driveEvent);
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
            name: "_instance",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        )!;
        IEventBus? previous = (IEventBus?)field.GetValue(obj: null);
        field.SetValue(obj: null, value: next);
        return previous;
    }

    [Theory]
    [InlineData(data: [DriveEventType.DriveAdded, "drive_added"])]
    [InlineData(data: [DriveEventType.DriveRemoved, "drive_removed"])]
    [InlineData(data: [DriveEventType.DiscInserted, "disc_inserted"])]
    [InlineData(data: [DriveEventType.DiscEjected, "disc_ejected"])]
    public async Task ExecuteAsync_PublishesWithTheExpectedWireMethodName(
        DriveEventType eventType,
        string expectedMethod
    )
    {
        RecordingEventBus eventBus = new();
        IEventBus? original = SwapEventBus(next: eventBus);
        try
        {
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(expression: m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(value: Events(types: eventType));
            DriveMonitorWorker worker = new(
                driveMonitor: driveMonitor.Object,
                logger: NullLogger<DriveMonitorWorker>.Instance
            );

            await worker.StartAsync(cancellationToken: CancellationToken.None);
            await Task.Delay(millisecondsDelay: 50);

            Assert.Single(collection: eventBus.Published);
            Assert.Equal(expected: expectedMethod, actual: eventBus.Published[index: 0].DriveStateData.Method);
            Assert.Equal(expected: Drive.Path, actual: eventBus.Published[index: 0].DriveStateData.Drive);
            Assert.Equal(expected: Drive.Label, actual: eventBus.Published[index: 0].DriveStateData.VolumeLabel);
            Assert.True(condition: eventBus.Published[index: 0].DriveStateData.HasDisc);
            Assert.Equal(expected: "dvd", actual: eventBus.Published[index: 0].DriveStateData.DiscType);
        }
        finally
        {
            SwapEventBus(next: original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_EventBusNotConfigured_SkipsPublishingWithoutThrowing()
    {
        IEventBus? original = SwapEventBus(next: null);
        try
        {
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(expression: m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(value: Events(types: DriveEventType.DriveAdded));
            DriveMonitorWorker worker = new(
                driveMonitor: driveMonitor.Object,
                logger: NullLogger<DriveMonitorWorker>.Instance
            );

            Exception? thrown = await Record.ExceptionAsync(testCode: async () =>
            {
                await worker.StartAsync(cancellationToken: CancellationToken.None);
                await Task.Delay(millisecondsDelay: 50);
            });

            Assert.Null(@object: thrown);
        }
        finally
        {
            SwapEventBus(next: original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UnmappedEventType_FallsBackToDriveChangedMethod()
    {
        RecordingEventBus eventBus = new();
        IEventBus? original = SwapEventBus(next: eventBus);
        try
        {
            // Cast a value outside the declared DriveEventType range — the
            // switch's `_ => "drive_changed"` arm is the safety net for any
            // enum value the encoder layer adds later without a matching
            // case being added here.
            Mock<IDriveMonitor> driveMonitor = new();
            driveMonitor
                .Setup(expression: m => m.MonitorAsync(It.IsAny<CancellationToken>()))
                .Returns(value: Events(types: (DriveEventType)99));
            DriveMonitorWorker worker = new(
                driveMonitor: driveMonitor.Object,
                logger: NullLogger<DriveMonitorWorker>.Instance
            );

            await worker.StartAsync(cancellationToken: CancellationToken.None);
            await Task.Delay(millisecondsDelay: 50);

            Assert.Single(collection: eventBus.Published);
            Assert.Equal(expected: "drive_changed", actual: eventBus.Published[index: 0].DriveStateData.Method);
        }
        finally
        {
            SwapEventBus(next: original);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MonitorThrows_RetriesThenExitsCleanlyOnCancellation()
    {
        Mock<IDriveMonitor> driveMonitor = new();
        driveMonitor
            .Setup(expression: m => m.MonitorAsync(It.IsAny<CancellationToken>()))
            .Throws<InvalidOperationException>();
        DriveMonitorWorker worker = new(
            driveMonitor: driveMonitor.Object,
            logger: NullLogger<DriveMonitorWorker>.Instance
        );
        using CancellationTokenSource cts = new();
        cts.CancelAfter(delay: TimeSpan.FromMilliseconds(milliseconds: 50));

        Exception? thrown = await Record.ExceptionAsync(testCode: async () =>
        {
            await worker.StartAsync(cancellationToken: cts.Token);
            await Task.Delay(millisecondsDelay: 300);
        });

        // The 5s retry backoff itself is cut short by the token cancelling at
        // 50ms — reaching here without a real 5s wait proves the retry loop's
        // own OperationCanceledException catch (not the outer one) is what
        // exits the worker.
        Assert.Null(@object: thrown);
    }
}
