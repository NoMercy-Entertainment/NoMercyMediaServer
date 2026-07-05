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

using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Database.Models.Music;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Covers <see cref="MusicPlayerState.CloneForBroadcast"/>: the per-broadcast
/// projection must drop lyrics from every queue entry (the remote-action
/// latency culprit — a full lyric sheet per queued track on a ~5s position
/// broadcast) while keeping the current track's lyrics for instant render, and
/// must never mutate the stored state.
/// </summary>
[Trait("Category", "Unit")]
public class MusicBroadcastPayloadTests
{
    private static PlaylistTrackDto MakeTrackWithLyrics()
    {
        Track track = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Track",
            Duration = "180",
            Filename = "test.mp3",
            Folder = "/music/",
            FolderId = Ulid.NewUlid(),
            Lyrics =
            [
                new()
                {
                    Text = "first line",
                    Time = new() { Total = 1.0, Seconds = 1 },
                },
                new()
                {
                    Text = "second line",
                    Time = new() { Total = 2.5, Seconds = 2 },
                },
            ],
        };
        PlaylistTrackDto dto = new(track, "US");
        // The DTO copies Lyrics from the entity; guard the fixture in case that
        // path ever changes, so the strip assertion can't silently pass on null.
        Assert.NotNull(dto.Lyrics);
        return dto;
    }

    private static MusicPlayerState MakeStateWithQueueLyrics()
    {
        PlaylistTrackDto current = MakeTrackWithLyrics();
        return new()
        {
            DeviceId = "device-abc",
            VolumePercentage = 42,
            CurrentItem = current,
            Backlog = [MakeTrackWithLyrics(), current],
            Playlist = [MakeTrackWithLyrics(), MakeTrackWithLyrics()],
            CurrentList = new("/music/albums/x", UriKind.Relative),
        };
    }

    [Fact]
    public void CloneForBroadcast_StripsLyricsFromEveryQueueEntry()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.All(broadcast.Playlist, track => Assert.Null(track.Lyrics));
        Assert.All(broadcast.Backlog, track => Assert.Null(track.Lyrics));
    }

    [Fact]
    public void CloneForBroadcast_KeepsCurrentItemLyrics()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.NotNull(broadcast.CurrentItem);
        Assert.NotNull(broadcast.CurrentItem!.Lyrics);
        Assert.Equal(2, broadcast.CurrentItem.Lyrics!.Length);
    }

    [Fact]
    public void CloneForBroadcast_DoesNotMutateStoredState()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        state.CloneForBroadcast();

        // The stored queue must still carry lyrics — stripping happens only on
        // the throwaway copy, never on the state the server keeps authoring.
        Assert.All(state.Playlist, track => Assert.NotNull(track.Lyrics));
        Assert.All(state.Backlog, track => Assert.NotNull(track.Lyrics));
    }

    [Fact]
    public void CloneForBroadcast_StripsBacklogEntryThatSharesCurrentItemReference()
    {
        // HandleNewPlayerState seeds Backlog with the exact CurrentItem
        // reference. In-place stripping would blank CurrentItem's lyrics too;
        // record `with` copies must isolate the two.
        MusicPlayerState state = MakeStateWithQueueLyrics();
        Assert.Same(state.CurrentItem, state.Backlog[^1]);

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Null(broadcast.Backlog[^1].Lyrics);
        Assert.NotNull(broadcast.CurrentItem!.Lyrics);
    }

    [Fact]
    public void CloneForBroadcast_PreservesQueueOrderAndScalarFields()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();
        state.SetPosition(12_345);

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Equal(state.DeviceId, broadcast.DeviceId);
        Assert.Equal(state.VolumePercentage, broadcast.VolumePercentage);
        Assert.Equal(state.Time, broadcast.Time);
        Assert.Equal(state.PositionCapturedAtMs, broadcast.PositionCapturedAtMs);
        Assert.Equal(state.CurrentList, broadcast.CurrentList);
        Assert.Equal(
            state.Playlist.Select(track => track.Id),
            broadcast.Playlist.Select(track => track.Id)
        );
        Assert.Equal(
            state.Backlog.Select(track => track.Id),
            broadcast.Backlog.Select(track => track.Id)
        );
    }
}
