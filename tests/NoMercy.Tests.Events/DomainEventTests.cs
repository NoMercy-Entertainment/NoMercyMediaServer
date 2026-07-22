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
using NoMercy.Events.Configuration;
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Events.Playback;
using NoMercy.Events.Plugins;
using NoMercy.Events.Users;
using Xunit;

namespace NoMercy.Tests.Events;

public class DomainEventTests
{
    [Fact]
    public void MediaDiscoveredEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaDiscoveredEvent evt = new()
        {
            FilePath = "/media/movies/test.mkv",
            LibraryId = libraryId,
            DetectedType = "movie",
        };

        evt.Source.Should().Be(expected: "MediaScanner");
        evt.FilePath.Should().Be(expected: "/media/movies/test.mkv");
        evt.LibraryId.Should().Be(expected: libraryId);
        evt.DetectedType.Should().Be(expected: "movie");
        evt.EventId.Should().NotBe(unexpected: Guid.Empty);
        evt.Timestamp.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 1));
    }

    [Fact]
    public void MediaDiscoveredEvent_DetectedTypeIsOptional()
    {
        MediaDiscoveredEvent evt = new()
        {
            FilePath = "/media/test.mkv",
            LibraryId = Ulid.NewUlid(),
        };

        evt.DetectedType.Should().BeNull();
    }

    [Fact]
    public void MediaAddedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaAddedEvent evt = new()
        {
            MediaId = 12345,
            MediaType = "movie",
            Title = "Test Movie",
            LibraryId = libraryId,
        };

        evt.Source.Should().Be(expected: "MediaProcessor");
        evt.MediaId.Should().Be(expected: 12345);
        evt.MediaType.Should().Be(expected: "movie");
        evt.Title.Should().Be(expected: "Test Movie");
        evt.LibraryId.Should().Be(expected: libraryId);
    }

    [Fact]
    public void MediaRemovedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        MediaRemovedEvent evt = new()
        {
            MediaId = 99,
            MediaType = "tv",
            Title = "Test Show",
            LibraryId = libraryId,
        };

        evt.Source.Should().Be(expected: "MediaProcessor");
        evt.MediaId.Should().Be(expected: 99);
        evt.MediaType.Should().Be(expected: "tv");
        evt.Title.Should().Be(expected: "Test Show");
        evt.LibraryId.Should().Be(expected: libraryId);
    }

    [Fact]
    public void EncodingStartedEvent_SetsAllProperties()
    {
        EncodingStartedEvent evt = new()
        {
            JobId = 42,
            InputPath = "/input/video.mkv",
            OutputPath = "/output/video/",
            ProfileName = "HLS-1080p",
        };

        evt.Source.Should().Be(expected: "Encoder");
        evt.JobId.Should().Be(expected: 42);
        evt.InputPath.Should().Be(expected: "/input/video.mkv");
        evt.OutputPath.Should().Be(expected: "/output/video/");
        evt.ProfileName.Should().Be(expected: "HLS-1080p");
    }

    [Fact]
    public void EncodingProgressEvent_SetsAllProperties()
    {
        EncodingProgressUpdatedEvent evt = new()
        {
            JobId = 42,
            Percentage = 55.5,
            Elapsed = TimeSpan.FromMinutes(minutes: 10),
            Estimated = TimeSpan.FromMinutes(minutes: 8),
        };

        evt.Source.Should().Be(expected: "Encoder");
        evt.JobId.Should().Be(expected: 42);
        evt.Percentage.Should().Be(expected: 55.5);
        evt.Elapsed.Should().Be(expected: TimeSpan.FromMinutes(minutes: 10));
        evt.Estimated.Should().Be(expected: TimeSpan.FromMinutes(minutes: 8));
    }

    [Fact]
    public void EncodingProgressEvent_EstimatedIsOptional()
    {
        EncodingProgressUpdatedEvent evt = new()
        {
            JobId = 1,
            Percentage = 0.0,
            Elapsed = TimeSpan.Zero,
        };

        evt.Estimated.Should().BeNull();
    }

    [Fact]
    public void EncodingCompletedEvent_SetsAllProperties()
    {
        EncodingCompletedEvent evt = new()
        {
            JobId = 42,
            OutputPath = "/output/video/playlist.m3u8",
            Duration = TimeSpan.FromMinutes(minutes: 18),
        };

        evt.Source.Should().Be(expected: "Encoder");
        evt.JobId.Should().Be(expected: 42);
        evt.OutputPath.Should().Be(expected: "/output/video/playlist.m3u8");
        evt.Duration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 18));
    }

    [Fact]
    public void EncodingFailedEvent_SetsAllProperties()
    {
        EncodingFailedEvent evt = new()
        {
            JobId = 42,
            InputPath = "/input/corrupt.mkv",
            ErrorMessage = "FFmpeg exited with code 1",
            ExceptionType = "InvalidOperationException",
        };

        evt.Source.Should().Be(expected: "Encoder");
        evt.JobId.Should().Be(expected: 42);
        evt.InputPath.Should().Be(expected: "/input/corrupt.mkv");
        evt.ErrorMessage.Should().Be(expected: "FFmpeg exited with code 1");
        evt.ExceptionType.Should().Be(expected: "InvalidOperationException");
    }

    [Fact]
    public void EncodingFailedEvent_ExceptionTypeIsOptional()
    {
        EncodingFailedEvent evt = new()
        {
            JobId = 1,
            InputPath = "/input/test.mkv",
            ErrorMessage = "Unknown error",
        };

        evt.ExceptionType.Should().BeNull();
    }

    [Fact]
    public void UserAuthenticatedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        UserAuthenticatedEvent evt = new()
        {
            UserId = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
        };

        evt.Source.Should().Be(expected: "Auth");
        evt.UserId.Should().Be(expected: userId);
        evt.Email.Should().Be(expected: "user@example.com");
        evt.DisplayName.Should().Be(expected: "Test User");
    }

    [Fact]
    public void UserDisconnectedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        UserDisconnectedEvent evt = new() { UserId = userId, ConnectionId = "abc-123-def" };

        evt.Source.Should().Be(expected: "SignalR");
        evt.UserId.Should().Be(expected: userId);
        evt.ConnectionId.Should().Be(expected: "abc-123-def");
    }

    [Fact]
    public void PlaybackStartedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackStartedEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            MediaType = "movie",
            DeviceId = "device-001",
        };

        evt.Source.Should().Be(expected: "Playback");
        evt.UserId.Should().Be(expected: userId);
        evt.MediaId.Should().Be(expected: 500);
        evt.MediaType.Should().Be(expected: "movie");
        evt.DeviceId.Should().Be(expected: "device-001");
    }

    [Fact]
    public void PlaybackStartedEvent_DeviceIdIsOptional()
    {
        PlaybackStartedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "tv",
        };

        evt.DeviceId.Should().BeNull();
    }

    [Fact]
    public void PlaybackProgressEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackProgressUpdatedEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            Position = TimeSpan.FromMinutes(minutes: 45),
            Duration = TimeSpan.FromMinutes(minutes: 120),
        };

        evt.Source.Should().Be(expected: "Playback");
        evt.UserId.Should().Be(expected: userId);
        evt.MediaId.Should().Be(expected: 500);
        evt.Position.Should().Be(expected: TimeSpan.FromMinutes(minutes: 45));
        evt.Duration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 120));
    }

    [Fact]
    public void PlaybackCompletedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        PlaybackCompletedEvent evt = new()
        {
            UserId = userId,
            MediaId = 500,
            MediaType = "movie",
        };

        evt.Source.Should().Be(expected: "Playback");
        evt.UserId.Should().Be(expected: userId);
        evt.MediaId.Should().Be(expected: 500);
        evt.MediaType.Should().Be(expected: "movie");
    }

    [Fact]
    public void LibraryScanStartedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        LibraryScanStartedEvent evt = new() { LibraryId = libraryId, LibraryName = "Movies" };

        evt.Source.Should().Be(expected: "LibraryScanner");
        evt.LibraryId.Should().Be(expected: libraryId);
        evt.LibraryName.Should().Be(expected: "Movies");
    }

    [Fact]
    public void LibraryScanCompletedEvent_SetsAllProperties()
    {
        Ulid libraryId = Ulid.NewUlid();
        LibraryScanCompletedEvent evt = new()
        {
            LibraryId = libraryId,
            LibraryName = "Movies",
            ItemsFound = 150,
            Duration = TimeSpan.FromSeconds(seconds: 30),
        };

        evt.Source.Should().Be(expected: "LibraryScanner");
        evt.LibraryId.Should().Be(expected: libraryId);
        evt.LibraryName.Should().Be(expected: "Movies");
        evt.ItemsFound.Should().Be(expected: 150);
        evt.Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 30));
    }

    [Fact]
    public void PluginLoadedEvent_SetsAllProperties()
    {
        PluginLoadedEvent evt = new()
        {
            PluginId = "my-plugin",
            PluginName = "My Plugin",
            Version = "1.0.0",
        };

        evt.Source.Should().Be(expected: "PluginManager");
        evt.PluginId.Should().Be(expected: "my-plugin");
        evt.PluginName.Should().Be(expected: "My Plugin");
        evt.Version.Should().Be(expected: "1.0.0");
    }

    [Fact]
    public void PluginErrorEvent_SetsAllProperties()
    {
        PluginErrorOccurredEvent evt = new()
        {
            PluginId = "bad-plugin",
            PluginName = "Bad Plugin",
            ErrorMessage = "Failed to initialize",
            ExceptionType = "NullReferenceException",
        };

        evt.Source.Should().Be(expected: "PluginManager");
        evt.PluginId.Should().Be(expected: "bad-plugin");
        evt.PluginName.Should().Be(expected: "Bad Plugin");
        evt.ErrorMessage.Should().Be(expected: "Failed to initialize");
        evt.ExceptionType.Should().Be(expected: "NullReferenceException");
    }

    [Fact]
    public void PluginErrorEvent_ExceptionTypeIsOptional()
    {
        PluginErrorOccurredEvent evt = new()
        {
            PluginId = "x",
            PluginName = "X",
            ErrorMessage = "error",
        };

        evt.ExceptionType.Should().BeNull();
    }

    [Fact]
    public void ConfigurationChangedEvent_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        ConfigurationChangedEvent evt = new()
        {
            Section = "Encoding",
            Key = "DefaultProfile",
            ChangedByUserId = userId,
        };

        evt.Source.Should().Be(expected: "Configuration");
        evt.Section.Should().Be(expected: "Encoding");
        evt.Key.Should().Be(expected: "DefaultProfile");
        evt.ChangedByUserId.Should().Be(expected: userId);
    }

    [Fact]
    public void ConfigurationChangedEvent_ChangedByUserIdIsOptional()
    {
        ConfigurationChangedEvent evt = new() { Section = "System", Key = "Port" };

        evt.ChangedByUserId.Should().BeNull();
    }

    [Fact]
    public void AllDomainEvents_ImplementIEvent()
    {
        IEvent[] events =
        [
            new MediaDiscoveredEvent { FilePath = "/test", LibraryId = Ulid.NewUlid() },
            new MediaAddedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "T",
                LibraryId = Ulid.NewUlid(),
            },
            new MediaRemovedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "T",
                LibraryId = Ulid.NewUlid(),
            },
            new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/i",
                OutputPath = "/o",
                ProfileName = "p",
            },
            new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 0,
                Elapsed = TimeSpan.Zero,
            },
            new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/o",
                Duration = TimeSpan.Zero,
            },
            new EncodingFailedEvent
            {
                JobId = 1,
                InputPath = "/i",
                ErrorMessage = "e",
            },
            new UserAuthenticatedEvent
            {
                UserId = Guid.NewGuid(),
                Email = "a@b.c",
                DisplayName = "A",
            },
            new UserDisconnectedEvent { UserId = Guid.NewGuid(), ConnectionId = "c" },
            new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            },
            new PlaybackProgressUpdatedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero,
            },
            new PlaybackCompletedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            },
            new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "L" },
            new LibraryScanCompletedEvent
            {
                LibraryId = Ulid.NewUlid(),
                LibraryName = "L",
                ItemsFound = 0,
                Duration = TimeSpan.Zero,
            },
            new PluginLoadedEvent
            {
                PluginId = "p",
                PluginName = "P",
                Version = "1.0",
            },
            new PluginErrorOccurredEvent
            {
                PluginId = "p",
                PluginName = "P",
                ErrorMessage = "e",
            },
            new ConfigurationChangedEvent { Section = "s", Key = "k" },
        ];

        foreach (IEvent evt in events)
        {
            evt.EventId.Should().NotBe(unexpected: Guid.Empty);
            evt.Timestamp.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromSeconds(seconds: 5));
            evt.Source.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task AllDomainEvents_CanBePublishedViaEventBus()
    {
        InMemoryEventBus bus = new();
        List<IEvent> received = [];

        bus.Subscribe<MediaDiscoveredEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<MediaAddedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<EncodingStartedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );
        bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new MediaDiscoveredEvent { FilePath = "/test", LibraryId = Ulid.NewUlid() }
        );
        await bus.PublishAsync(
            @event: new MediaAddedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "T",
                LibraryId = Ulid.NewUlid(),
            }
        );
        await bus.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/i",
                OutputPath = "/o",
                ProfileName = "p",
            }
        );
        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = Ulid.NewUlid(),
                LibraryName = "L",
                ItemsFound = 5,
                Duration = TimeSpan.FromSeconds(seconds: 1),
            }
        );

        received.Should().HaveCount(expected: 4);
        received[index: 0].Should().BeOfType<MediaDiscoveredEvent>();
        received[index: 1].Should().BeOfType<MediaAddedEvent>();
        received[index: 2].Should().BeOfType<EncodingStartedEvent>();
        received[index: 3].Should().BeOfType<LibraryScanCompletedEvent>();
    }
}
