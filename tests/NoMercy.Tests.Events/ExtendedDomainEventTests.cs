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

using FluentAssertions;
using NoMercy.Events;
using NoMercy.Events.Cast;
using NoMercy.Events.DriveMonitor;
using NoMercy.Events.Inbox;
using NoMercy.Events.Library;
using NoMercy.Events.Music;
using NoMercy.Events.Users;
using Xunit;

namespace NoMercy.Tests.Events;

public class ExtendedDomainEventTests
{
    [Fact]
    public async Task CastDeviceStatusChangedEvent_HandlerReceivesExactPayload()
    {
        InMemoryEventBus bus = new();
        CastDeviceStatusChangedEvent? captured = null;

        bus.Subscribe<CastDeviceStatusChangedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Dictionary<string, object?> statusData = new()
        {
            [key: "volume"] = 0.75,
            [key: "muted"] = false,
            [key: "state"] = "PLAYING",
        };

        CastDeviceStatusChangedEvent published = new()
        {
            EventType = "mediaStatus",
            StatusData = statusData,
        };

        await bus.PublishAsync(@event: published);

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(expected: "mediaStatus");
        captured.StatusData.Should().ContainKey(expected: "volume").WhoseValue.Should().Be(expected: 0.75);
        captured.StatusData.Should().ContainKey(expected: "state").WhoseValue.Should().Be(expected: "PLAYING");
        captured.Source.Should().Be(expected: "ChromeCast");
        captured.EventId.Should().Be(expected: published.EventId);
    }

    [Fact]
    public async Task DriveStateChangedEvent_DiscInserted_HandlerReceivesDiscType()
    {
        InMemoryEventBus bus = new();
        DriveStateChangedEvent? captured = null;

        bus.Subscribe<DriveStateChangedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        DriveStatePayload payload = new(
            Method: "disc_inserted",
            Drive: "D:\\",
            VolumeLabel: "MOVIE_TITLE",
            HasDisc: true,
            DiscType: "bluray",
            Timestamp: DateTime.UtcNow
        );

        await bus.PublishAsync(@event: new DriveStateChangedEvent { DriveStateData = payload });

        captured.Should().NotBeNull();
        captured!.DriveStateData.Method.Should().Be(expected: "disc_inserted");
        captured.DriveStateData.Drive.Should().Be(expected: "D:\\");
        captured.DriveStateData.VolumeLabel.Should().Be(expected: "MOVIE_TITLE");
        captured.DriveStateData.HasDisc.Should().BeTrue();
        captured.DriveStateData.DiscType.Should().Be(expected: "bluray");
        captured.Source.Should().Be(expected: "DriveMonitor");
    }

    [Fact]
    public async Task DriveStateChangedEvent_RipProgress_PreservesJobIdAndMessage()
    {
        InMemoryEventBus bus = new();
        DriveStateChangedEvent? captured = null;

        bus.Subscribe<DriveStateChangedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        DriveStatePayload payload = new(
            Method: "rip_progress",
            Drive: "/dev/sr0",
            VolumeLabel: "DISC_LABEL",
            HasDisc: true,
            DiscType: "dvd",
            Timestamp: DateTime.UtcNow,
            JobId: "job-abc-123",
            Message: "Ripping track 3 of 12"
        );

        await bus.PublishAsync(@event: new DriveStateChangedEvent { DriveStateData = payload });

        captured!.DriveStateData.JobId.Should().Be(expected: "job-abc-123");
        captured.DriveStateData.Message.Should().Be(expected: "Ripping track 3 of 12");
    }

    [Fact]
    public async Task InboxItemDetectedEvent_HandlerReceivesExactPayload()
    {
        InMemoryEventBus bus = new();
        InboxItemDetectedEvent? captured = null;

        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new InboxItemDetectedEvent
            {
                Id = "inbox-item-001",
                DetectedType = "movie",
                Confidence = "high",
                Status = "pending",
            }
        );

        captured.Should().NotBeNull();
        captured!.Id.Should().Be(expected: "inbox-item-001");
        captured.DetectedType.Should().Be(expected: "movie");
        captured.Confidence.Should().Be(expected: "high");
        captured.Status.Should().Be(expected: "pending");
        captured.Source.Should().Be(expected: "Inbox");
    }

    [Fact]
    public async Task InboxItemUpdatedEvent_HandlerReceivesStatusTransition()
    {
        InMemoryEventBus bus = new();
        List<InboxItemUpdatedEvent> received = [];

        bus.Subscribe<InboxItemUpdatedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new InboxItemUpdatedEvent { Id = "inbox-item-001", Status = "processing" }
        );
        await bus.PublishAsync(
            @event: new InboxItemUpdatedEvent { Id = "inbox-item-001", Status = "completed" }
        );

        received.Should().HaveCount(expected: 2);
        received[index: 0].Status.Should().Be(expected: "processing");
        received[index: 1].Status.Should().Be(expected: "completed");
        received[index: 0].Id.Should().Be(expected: "inbox-item-001");
        received[index: 1].Id.Should().Be(expected: "inbox-item-001");
        received[index: 0].Source.Should().Be(expected: "Inbox");
    }

    [Fact]
    public async Task UserPermissionsChangedEvent_HandlerReceivesExactUserAndChanger()
    {
        InMemoryEventBus bus = new();
        UserPermissionsChangedEvent? captured = null;

        bus.Subscribe<UserPermissionsChangedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Guid targetUser = Guid.NewGuid();
        Guid adminUser = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new UserPermissionsChangedEvent { UserId = targetUser, ChangedBy = adminUser }
        );

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(expected: targetUser);
        captured.ChangedBy.Should().Be(expected: adminUser);
        captured.Source.Should().Be(expected: "Users");
    }

    [Fact]
    public async Task UserDisconnectedEvent_HandlerReceivesConnectionId()
    {
        InMemoryEventBus bus = new();
        UserDisconnectedEvent? captured = null;

        bus.Subscribe<UserDisconnectedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new UserDisconnectedEvent { UserId = userId, ConnectionId = "conn-xyz-789" }
        );

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(expected: userId);
        captured.ConnectionId.Should().Be(expected: "conn-xyz-789");
        captured.Source.Should().Be(expected: "SignalR");
    }

    [Fact]
    public async Task LibraryDeletedEvent_HandlerReceivesLibraryName()
    {
        InMemoryEventBus bus = new();
        LibraryDeletedEvent? captured = null;

        bus.Subscribe<LibraryDeletedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(
            @event: new LibraryDeletedEvent { LibraryId = libraryId, LibraryName = "Old Movies" }
        );

        captured.Should().NotBeNull();
        captured!.LibraryId.Should().Be(expected: libraryId);
        captured.LibraryName.Should().Be(expected: "Old Movies");
        captured.Source.Should().Be(expected: "Library");
    }

    [Fact]
    public async Task FolderPathAddedEvent_HandlerReceivesSubPath()
    {
        InMemoryEventBus bus = new();
        FolderPathAddedEvent? captured = null;

        bus.Subscribe<FolderPathAddedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid requestPath = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        await bus.PublishAsync(
            @event: new FolderPathAddedEvent
            {
                RequestPath = requestPath,
                DriverId = driverId,
                SubPath = "Movies/Action",
            }
        );

        captured.Should().NotBeNull();
        captured!.RequestPath.Should().Be(expected: requestPath);
        captured.DriverId.Should().Be(expected: driverId);
        captured.SubPath.Should().Be(expected: "Movies/Action");
        captured.Source.Should().Be(expected: "Library");
    }

    [Fact]
    public async Task FolderPathRemovedEvent_HandlerReceivesRequestPath()
    {
        InMemoryEventBus bus = new();
        FolderPathRemovedEvent? captured = null;

        bus.Subscribe<FolderPathRemovedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid requestPath = Ulid.NewUlid();

        await bus.PublishAsync(@event: new FolderPathRemovedEvent { RequestPath = requestPath });

        captured.Should().NotBeNull();
        captured!.RequestPath.Should().Be(expected: requestPath);
        captured.Source.Should().Be(expected: "Library");
    }

    [Fact]
    public async Task MediaFilesScannedEvent_HandlerReceivesMediaIdAndLibrary()
    {
        InMemoryEventBus bus = new();
        MediaFilesScannedEvent? captured = null;

        bus.Subscribe<MediaFilesScannedEvent>(
            handler: (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(
            @event: new MediaFilesScannedEvent { MediaId = 4217, LibraryId = libraryId }
        );

        captured.Should().NotBeNull();
        captured!.MediaId.Should().Be(expected: 4217);
        captured.LibraryId.Should().Be(expected: libraryId);
        captured.Source.Should().Be(expected: "FileRescan");
    }

    [Fact]
    public async Task MusicItemLikedEvent_HandlerReceivesLikedStateToggle()
    {
        InMemoryEventBus bus = new();
        List<MusicItemLikedEvent> received = [];

        bus.Subscribe<MusicItemLikedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new MusicItemLikedEvent
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = "track",
                Liked = true,
            }
        );
        await bus.PublishAsync(
            @event: new MusicItemLikedEvent
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = "track",
                Liked = false,
            }
        );

        received.Should().HaveCount(expected: 2);
        received[index: 0].Liked.Should().BeTrue();
        received[index: 1].Liked.Should().BeFalse();
        received[index: 0].ItemType.Should().Be(expected: "track");
        received[index: 0].UserId.Should().Be(expected: userId);
        received[index: 0].ItemId.Should().Be(expected: itemId);
        received[index: 0].Source.Should().Be(expected: "Music");
    }

    [Fact]
    public async Task MultipleUncoveredEventTypes_AllRoutedCorrectly_OnSingleBus()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<CastDeviceStatusChangedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<DriveStateChangedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<InboxItemDetectedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<UserPermissionsChangedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new CastDeviceStatusChangedEvent { EventType = "status", StatusData = new() }
        );
        await bus.PublishAsync(
            @event: new DriveStateChangedEvent
            {
                DriveStateData = new(Method: "drive_added", Drive: "E:\\", VolumeLabel: null, HasDisc: false, DiscType: "none", Timestamp: DateTime.UtcNow),
            }
        );
        await bus.PublishAsync(
            @event: new InboxItemDetectedEvent
            {
                Id = "item-1",
                DetectedType = "tv",
                Confidence = "medium",
                Status = "pending",
            }
        );
        await bus.PublishAsync(
            @event: new UserPermissionsChangedEvent { UserId = Guid.NewGuid(), ChangedBy = Guid.NewGuid() }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<CastDeviceStatusChangedEvent>();
        received[index: 1].Should().BeOfType<DriveStateChangedEvent>();
        received[index: 2].Should().BeOfType<InboxItemDetectedEvent>();
        received[index: 3].Should().BeOfType<UserPermissionsChangedEvent>();
    }
}
