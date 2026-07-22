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

using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Database;
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
[Trait(name: "Category", value: "Unit")]
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
        PlaylistTrackDto dto = new(track: track, country: "US");
        // The DTO copies Lyrics from the entity; guard the fixture in case that
        // path ever changes, so the strip assertion can't silently pass on null.
        Assert.NotNull(@object: dto.Lyrics);
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
            CurrentList = new(uriString: "/music/albums/x", uriKind: UriKind.Relative),
        };
    }

    [Fact]
    public void CloneForBroadcast_StripsLyricsFromEveryQueueEntry()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.All(collection: broadcast.Playlist, action: track => Assert.Null(@object: track.Lyrics));
        Assert.All(collection: broadcast.Backlog, action: track => Assert.Null(@object: track.Lyrics));
    }

    [Fact]
    public void CloneForBroadcast_KeepsCurrentItemLyrics()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.NotNull(@object: broadcast.CurrentItem);
        Assert.NotNull(@object: broadcast.CurrentItem!.Lyrics);
        Assert.Equal(expected: 2, actual: broadcast.CurrentItem.Lyrics!.Length);
    }

    [Fact]
    public void CloneForBroadcast_DoesNotMutateStoredState()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        state.CloneForBroadcast();

        // The stored queue must still carry lyrics — stripping happens only on
        // the throwaway copy, never on the state the server keeps authoring.
        Assert.All(collection: state.Playlist, action: track => Assert.NotNull(@object: track.Lyrics));
        Assert.All(collection: state.Backlog, action: track => Assert.NotNull(@object: track.Lyrics));
    }

    [Fact]
    public void CloneForBroadcast_StripsBacklogEntryThatSharesCurrentItemReference()
    {
        // HandleNewPlayerState seeds Backlog with the exact CurrentItem
        // reference. In-place stripping would blank CurrentItem's lyrics too;
        // record `with` copies must isolate the two.
        MusicPlayerState state = MakeStateWithQueueLyrics();
        Assert.Same(expected: state.CurrentItem, actual: state.Backlog[^1]);

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Null(@object: broadcast.Backlog[^1].Lyrics);
        Assert.NotNull(@object: broadcast.CurrentItem!.Lyrics);
    }

    [Fact]
    public void CloneForBroadcast_PreservesQueueOrderAndScalarFields()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();
        state.SetPosition(positionMs: 12_345);

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Equal(expected: state.DeviceId, actual: broadcast.DeviceId);
        Assert.Equal(expected: state.VolumePercentage, actual: broadcast.VolumePercentage);
        Assert.Equal(expected: state.Time, actual: broadcast.Time);
        Assert.Equal(expected: state.PositionCapturedAtMs, actual: broadcast.PositionCapturedAtMs);
        Assert.Equal(expected: state.CurrentList, actual: broadcast.CurrentList);
        Assert.Equal(
            expected: state.Playlist.Select(selector: track => track.Id),
            actual: broadcast.Playlist.Select(selector: track => track.Id)
        );
        Assert.Equal(
            expected: state.Backlog.Select(selector: track => track.Id),
            actual: broadcast.Backlog.Select(selector: track => track.Id)
        );
    }

    // ── Palette + description strip ───────────────────────────────────────────
    // On a long queue (e.g. a whole genre) the palette graph is the dominant
    // wire weight the ~5s broadcast otherwise re-sends per track. A memory-tight
    // client re-parses the whole blob every tick and thrashes GC to the point
    // the playback service dies and the activity restarts — so the broadcast
    // projection drops the track palette AND the palette + unbounded description
    // on every nested album/artist, exactly as it already drops lyrics.

    private static AlbumDto MakeAlbumDtoWithHeavyFields()
    {
        Album album = new() { Id = Guid.NewGuid(), Name = "Test Album" };
        return new(album: album, country: "US")
        {
            ColorPalette = JToken.Parse(json: "[\"#ffffff\",\"#000000\"]"),
            Description = "an album bio no queue row renders",
        };
    }

    private static ArtistDto MakeArtistDtoWithHeavyFields()
    {
        Artist artist = new() { Id = Guid.NewGuid(), Name = "Test Artist" };
        ArtistTrack artistTrack = new()
        {
            Artist = artist,
            ArtistId = artist.Id,
            TrackId = Guid.NewGuid(),
        };
        return new(artistTrack: artistTrack, country: "US")
        {
            ColorPalette = JToken.Parse(json: "[\"#ffffff\"]"),
            Description = "an artist bio no queue row renders",
        };
    }

    private static PlaylistTrackDto MakeTrackWithHeavyQueueFields()
    {
        PlaylistTrackDto dto = MakeTrackWithLyrics();
        dto.ColorPalette = new ColorPalette();
        dto.Album = [MakeAlbumDtoWithHeavyFields()];
        dto.Artist = [MakeArtistDtoWithHeavyFields()];
        return dto;
    }

    private static MusicPlayerState MakeStateWithHeavyQueue()
    {
        PlaylistTrackDto current = MakeTrackWithHeavyQueueFields();
        return new()
        {
            DeviceId = "device-abc",
            CurrentItem = current,
            Backlog = [MakeTrackWithHeavyQueueFields(), current],
            Playlist = [MakeTrackWithHeavyQueueFields(), MakeTrackWithHeavyQueueFields()],
            CurrentList = new(uriString: "/music/genres/x", uriKind: UriKind.Relative),
        };
    }

    [Fact]
    public void CloneForBroadcast_StripsTrackColorPaletteFromEveryQueueEntry()
    {
        MusicPlayerState state = MakeStateWithHeavyQueue();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.All(collection: broadcast.Playlist, action: track => Assert.Null(@object: track.ColorPalette));
        Assert.All(collection: broadcast.Backlog, action: track => Assert.Null(@object: track.ColorPalette));
    }

    [Fact]
    public void CloneForBroadcast_StripsNestedAlbumAndArtistPaletteAndDescription()
    {
        MusicPlayerState state = MakeStateWithHeavyQueue();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        foreach (PlaylistTrackDto track in broadcast.Playlist.Concat(second: broadcast.Backlog))
        {
            Assert.All(
                collection: track.Album,
                action: album =>
                {
                    Assert.Null(@object: album.ColorPalette);
                    Assert.Null(@object: album.Description);
                }
            );
            Assert.All(
                collection: track.Artist,
                action: artist =>
                {
                    Assert.Null(@object: artist.ColorPalette);
                    Assert.Null(@object: artist.Description);
                }
            );
        }
    }

    [Fact]
    public void CloneForBroadcast_KeepsCurrentItemPaletteGraph()
    {
        MusicPlayerState state = MakeStateWithHeavyQueue();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.NotNull(@object: broadcast.CurrentItem);
        Assert.NotNull(@object: broadcast.CurrentItem!.ColorPalette);
        Assert.All(collection: broadcast.CurrentItem.Album, action: album => Assert.NotNull(@object: album.ColorPalette));
        Assert.All(collection: broadcast.CurrentItem.Artist, action: artist => Assert.NotNull(@object: artist.ColorPalette));
    }

    [Fact]
    public void CloneForBroadcast_DoesNotMutateStoredPaletteGraph()
    {
        MusicPlayerState state = MakeStateWithHeavyQueue();

        state.CloneForBroadcast();

        Assert.All(
            collection: state.Playlist,
            action: track =>
            {
                Assert.NotNull(@object: track.ColorPalette);
                Assert.All(collection: track.Album, action: album => Assert.NotNull(@object: album.ColorPalette));
                Assert.All(collection: track.Artist, action: artist => Assert.NotNull(@object: artist.ColorPalette));
            }
        );
    }

    [Fact]
    public void AlbumDto_ForBroadcastQueueEntry_NullsPaletteAndDescription_PreservesIdentity()
    {
        AlbumDto album = MakeAlbumDtoWithHeavyFields();

        AlbumDto stripped = album.ForBroadcastQueueEntry();

        Assert.Null(@object: stripped.ColorPalette);
        Assert.Null(@object: stripped.Description);
        Assert.Equal(expected: album.Id, actual: stripped.Id);
        Assert.Equal(expected: album.Name, actual: stripped.Name);
        Assert.Equal(expected: album.Link, actual: stripped.Link);
        // The source DTO the server keeps is never mutated.
        Assert.NotNull(@object: album.ColorPalette);
        Assert.NotNull(@object: album.Description);
    }

    [Fact]
    public void ArtistDto_ForBroadcastQueueEntry_NullsPaletteAndDescription_PreservesIdentity()
    {
        ArtistDto artist = MakeArtistDtoWithHeavyFields();

        ArtistDto stripped = artist.ForBroadcastQueueEntry();

        Assert.Null(@object: stripped.ColorPalette);
        Assert.Null(@object: stripped.Description);
        Assert.Equal(expected: artist.Id, actual: stripped.Id);
        Assert.Equal(expected: artist.Name, actual: stripped.Name);
        Assert.Equal(expected: artist.Link, actual: stripped.Link);
        Assert.NotNull(@object: artist.ColorPalette);
        Assert.NotNull(@object: artist.Description);
    }

    // ── Queue window ──────────────────────────────────────────────────────────
    // A whole-genre queue is ~8k tracks; serializing all of them into every ~5s
    // broadcast cost ~2s per emit and made play/pause land seconds late. The
    // broadcast carries only a window: the next N upcoming + last M played. The
    // server keeps the full lists for auto-advance/previous.

    [Fact]
    public void CloneForBroadcast_WindowsPlaylistToUpcomingCap()
    {
        List<PlaylistTrackDto> longPlaylist = Enumerable
            .Range(start: 0, count: 250)
            .Select(selector: _ => MakeTrackWithLyrics())
            .ToList();
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrackWithLyrics(),
            Playlist = longPlaylist,
            Backlog = [],
            CurrentList = new(uriString: "/music/genres/x", uriKind: UriKind.Relative),
        };

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Equal(expected: 100, actual: broadcast.Playlist.Count);
        // Front-anchored: the window is the first 100 upcoming tracks, in order.
        Assert.Equal(
            expected: longPlaylist.Take(count: 100).Select(selector: track => track.Id),
            actual: broadcast.Playlist.Select(selector: track => track.Id)
        );
        // The server's own stored playlist is never truncated.
        Assert.Equal(expected: 250, actual: state.Playlist.Count);
    }

    [Fact]
    public void CloneForBroadcast_WindowsBacklogToMostRecentCap()
    {
        List<PlaylistTrackDto> longBacklog = Enumerable
            .Range(start: 0, count: 80)
            .Select(selector: _ => MakeTrackWithLyrics())
            .ToList();
        MusicPlayerState state = new()
        {
            CurrentItem = MakeTrackWithLyrics(),
            Playlist = [],
            Backlog = longBacklog,
            CurrentList = new(uriString: "/music/genres/x", uriKind: UriKind.Relative),
        };

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Equal(expected: 20, actual: broadcast.Backlog.Count);
        // Back-anchored: the window is the last 20 played tracks, in order.
        Assert.Equal(
            expected: longBacklog.TakeLast(count: 20).Select(selector: track => track.Id),
            actual: broadcast.Backlog.Select(selector: track => track.Id)
        );
        Assert.Equal(expected: 80, actual: state.Backlog.Count);
    }

    [Fact]
    public void CloneForBroadcast_KeepsQueueWholeWhenUnderTheWindow()
    {
        MusicPlayerState state = MakeStateWithQueueLyrics();

        MusicPlayerState broadcast = state.CloneForBroadcast();

        Assert.Equal(expected: state.Playlist.Count, actual: broadcast.Playlist.Count);
        Assert.Equal(expected: state.Backlog.Count, actual: broadcast.Backlog.Count);
    }
}
