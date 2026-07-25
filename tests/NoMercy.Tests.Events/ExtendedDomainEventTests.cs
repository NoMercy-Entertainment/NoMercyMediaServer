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
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Dictionary<string, object?> statusData = new()
        {
            ["volume"] = 0.75,
            ["muted"] = false,
            ["state"] = "PLAYING",
        };

        CastDeviceStatusChangedEvent published = new()
        {
            EventType = "mediaStatus",
            StatusData = statusData,
        };

        await bus.PublishAsync(published);

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be("mediaStatus");
        captured.StatusData.Should().ContainKey("volume").WhoseValue.Should().Be(0.75);
        captured.StatusData.Should().ContainKey("state").WhoseValue.Should().Be("PLAYING");
        captured.Source.Should().Be("ChromeCast");
        captured.EventId.Should().Be(published.EventId);
    }

    [Fact]
    public async Task DriveStateChangedEvent_DiscInserted_HandlerReceivesDiscType()
    {
        InMemoryEventBus bus = new();
        DriveStateChangedEvent? captured = null;

        bus.Subscribe<DriveStateChangedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        DriveStatePayload payload = new(
            "disc_inserted",
            "D:\\",
            "MOVIE_TITLE",
            true,
            "bluray",
            DateTime.UtcNow
        );

        await bus.PublishAsync(new DriveStateChangedEvent { DriveStateData = payload });

        captured.Should().NotBeNull();
        captured!.DriveStateData.Method.Should().Be("disc_inserted");
        captured.DriveStateData.Drive.Should().Be("D:\\");
        captured.DriveStateData.VolumeLabel.Should().Be("MOVIE_TITLE");
        captured.DriveStateData.HasDisc.Should().BeTrue();
        captured.DriveStateData.DiscType.Should().Be("bluray");
        captured.Source.Should().Be("DriveMonitor");
    }

    [Fact]
    public async Task DriveStateChangedEvent_RipProgress_PreservesJobIdAndMessage()
    {
        InMemoryEventBus bus = new();
        DriveStateChangedEvent? captured = null;

        bus.Subscribe<DriveStateChangedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        DriveStatePayload payload = new(
            "rip_progress",
            "/dev/sr0",
            "DISC_LABEL",
            true,
            "dvd",
            DateTime.UtcNow,
            "job-abc-123",
            "Ripping track 3 of 12"
        );

        await bus.PublishAsync(new DriveStateChangedEvent { DriveStateData = payload });

        captured!.DriveStateData.JobId.Should().Be("job-abc-123");
        captured.DriveStateData.Message.Should().Be("Ripping track 3 of 12");
    }

    [Fact]
    public async Task InboxItemDetectedEvent_HandlerReceivesExactPayload()
    {
        InMemoryEventBus bus = new();
        InboxItemDetectedEvent? captured = null;

        bus.Subscribe<InboxItemDetectedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            new InboxItemDetectedEvent
            {
                Id = "inbox-item-001",
                DetectedType = "movie",
                Confidence = "high",
                Status = "pending",
            }
        );

        captured.Should().NotBeNull();
        captured!.Id.Should().Be("inbox-item-001");
        captured.DetectedType.Should().Be("movie");
        captured.Confidence.Should().Be("high");
        captured.Status.Should().Be("pending");
        captured.Source.Should().Be("Inbox");
    }

    [Fact]
    public async Task InboxItemUpdatedEvent_HandlerReceivesStatusTransition()
    {
        InMemoryEventBus bus = new();
        List<InboxItemUpdatedEvent> received = [];

        bus.Subscribe<InboxItemUpdatedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            new InboxItemUpdatedEvent { Id = "inbox-item-001", Status = "processing" }
        );
        await bus.PublishAsync(
            new InboxItemUpdatedEvent { Id = "inbox-item-001", Status = "completed" }
        );

        received.Should().HaveCount(2);
        received[0].Status.Should().Be("processing");
        received[1].Status.Should().Be("completed");
        received[0].Id.Should().Be("inbox-item-001");
        received[1].Id.Should().Be("inbox-item-001");
        received[0].Source.Should().Be("Inbox");
    }

    [Fact]
    public async Task UserPermissionsChangedEvent_HandlerReceivesExactUserAndChanger()
    {
        InMemoryEventBus bus = new();
        UserPermissionsChangedEvent? captured = null;

        bus.Subscribe<UserPermissionsChangedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Guid targetUser = Guid.NewGuid();
        Guid adminUser = Guid.NewGuid();

        await bus.PublishAsync(
            new UserPermissionsChangedEvent { UserId = targetUser, ChangedBy = adminUser }
        );

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(targetUser);
        captured.ChangedBy.Should().Be(adminUser);
        captured.Source.Should().Be("Users");
    }

    [Fact]
    public async Task UserDisconnectedEvent_HandlerReceivesConnectionId()
    {
        InMemoryEventBus bus = new();
        UserDisconnectedEvent? captured = null;

        bus.Subscribe<UserDisconnectedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            new UserDisconnectedEvent { UserId = userId, ConnectionId = "conn-xyz-789" }
        );

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.ConnectionId.Should().Be("conn-xyz-789");
        captured.Source.Should().Be("SignalR");
    }

    [Fact]
    public async Task LibraryDeletedEvent_HandlerReceivesLibraryName()
    {
        InMemoryEventBus bus = new();
        LibraryDeletedEvent? captured = null;

        bus.Subscribe<LibraryDeletedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(
            new LibraryDeletedEvent { LibraryId = libraryId, LibraryName = "Old Movies" }
        );

        captured.Should().NotBeNull();
        captured!.LibraryId.Should().Be(libraryId);
        captured.LibraryName.Should().Be("Old Movies");
        captured.Source.Should().Be("Library");
    }

    [Fact]
    public async Task FolderPathAddedEvent_HandlerReceivesSubPath()
    {
        InMemoryEventBus bus = new();
        FolderPathAddedEvent? captured = null;

        bus.Subscribe<FolderPathAddedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid requestPath = Ulid.NewUlid();
        Ulid driverId = Ulid.NewUlid();

        await bus.PublishAsync(
            new FolderPathAddedEvent
            {
                RequestPath = requestPath,
                DriverId = driverId,
                SubPath = "Movies/Action",
            }
        );

        captured.Should().NotBeNull();
        captured!.RequestPath.Should().Be(requestPath);
        captured.DriverId.Should().Be(driverId);
        captured.SubPath.Should().Be("Movies/Action");
        captured.Source.Should().Be("Library");
    }

    [Fact]
    public async Task FolderPathRemovedEvent_HandlerReceivesRequestPath()
    {
        InMemoryEventBus bus = new();
        FolderPathRemovedEvent? captured = null;

        bus.Subscribe<FolderPathRemovedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid requestPath = Ulid.NewUlid();

        await bus.PublishAsync(new FolderPathRemovedEvent { RequestPath = requestPath });

        captured.Should().NotBeNull();
        captured!.RequestPath.Should().Be(requestPath);
        captured.Source.Should().Be("Library");
    }

    [Fact]
    public async Task MediaFilesScannedEvent_HandlerReceivesMediaIdAndLibrary()
    {
        InMemoryEventBus bus = new();
        MediaFilesScannedEvent? captured = null;

        bus.Subscribe<MediaFilesScannedEvent>(
            (evt, _) =>
            {
                captured = evt;
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 4217, LibraryId = libraryId }
        );

        captured.Should().NotBeNull();
        captured!.MediaId.Should().Be(4217);
        captured.LibraryId.Should().Be(libraryId);
        captured.Source.Should().Be("FileRescan");
    }

    [Fact]
    public async Task MusicItemLikedEvent_HandlerReceivesLikedStateToggle()
    {
        InMemoryEventBus bus = new();
        List<MusicItemLikedEvent> received = [];

        bus.Subscribe<MusicItemLikedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();
        Guid itemId = Guid.NewGuid();

        await bus.PublishAsync(
            new MusicItemLikedEvent
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = "track",
                Liked = true,
            }
        );
        await bus.PublishAsync(
            new MusicItemLikedEvent
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = "track",
                Liked = false,
            }
        );

        received.Should().HaveCount(2);
        received[0].Liked.Should().BeTrue();
        received[1].Liked.Should().BeFalse();
        received[0].ItemType.Should().Be("track");
        received[0].UserId.Should().Be(userId);
        received[0].ItemId.Should().Be(itemId);
        received[0].Source.Should().Be("Music");
    }

    [Fact]
    public async Task MultipleUncoveredEventTypes_AllRoutedCorrectly_OnSingleBus()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<CastDeviceStatusChangedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<DriveStateChangedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<InboxItemDetectedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<UserPermissionsChangedEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            new CastDeviceStatusChangedEvent { EventType = "status", StatusData = new() }
        );
        await bus.PublishAsync(
            new DriveStateChangedEvent
            {
                DriveStateData = new("drive_added", "E:\\", null, false, "none", DateTime.UtcNow),
            }
        );
        await bus.PublishAsync(
            new InboxItemDetectedEvent
            {
                Id = "item-1",
                DetectedType = "tv",
                Confidence = "medium",
                Status = "pending",
            }
        );
        await bus.PublishAsync(
            new UserPermissionsChangedEvent { UserId = Guid.NewGuid(), ChangedBy = Guid.NewGuid() }
        );

        received.Should().HaveCount(4);
        received[0].Should().BeOfType<CastDeviceStatusChangedEvent>();
        received[1].Should().BeOfType<DriveStateChangedEvent>();
        received[2].Should().BeOfType<InboxItemDetectedEvent>();
        received[3].Should().BeOfType<UserPermissionsChangedEvent>();
    }
}
