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

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.Services.Video;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;
using Client = NoMercy.Networking.Http.Client;

namespace NoMercy.Tests.Api.Services.Video;

/// <summary>
/// Requirement-driven coverage for every command branch VideoPlaybackCommandHandler
/// dispatches from VideoHub.PlaybackCommand: play/pause/seek/item/episode/forward/
/// backward/next/previous/nextChapter/previousChapter/stop/mute/volume/audio/
/// cycleAudio/caption/cycleCaption/quality, plus the unknown-command fallthrough.
/// The handler is resolved from the real DI container (it is registered as a
/// singleton in VideoHubServiceExtensions) so IServiceScopeFactory / IDbContextFactory
/// wiring matches production, and preference-persisting branches exercise the
/// real seeded SQLite database rather than a mock.
/// </summary>
[Trait(name: "Category", value: "Playback")]
public class VideoPlaybackCommandHandlerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public VideoPlaybackCommandHandlerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    private VideoPlaybackCommandHandler CreateHandler() =>
        _factory.Services.GetRequiredService<VideoPlaybackCommandHandler>();

    private static User SeededUser() =>
        new()
        {
            Id = TestAuthHandler.DefaultUserId,
            Email = TestAuthHandler.DefaultUserEmail,
            Name = TestAuthHandler.DefaultUserName,
            Owner = true,
            Allowed = true,
            Manage = true,
        };

    private async Task<Ulid> SeededVideoFileIdAsync(int movieId)
    {
        IDbContextFactory<MediaContext> contextFactory = _factory.Services.GetRequiredService<
            IDbContextFactory<MediaContext>
        >();
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        return await ctx.VideoFiles.Where(predicate: v => v.MovieId == movieId).Select(selector: v => v.Id).FirstAsync();
    }

    private async Task<VideoPlaylistResponseDto> BuildMovieItemAsync(
        int movieId,
        string duration = "0:10:00"
    )
    {
        return new()
        {
            Id = movieId,
            TmdbId = movieId,
            Title = $"Movie {movieId}",
            PlaylistType = MediaTypes.MovieMediaType,
            LibraryType = MediaTypes.MovieMediaType,
            PlaylistId = movieId,
            VideoId = await SeededVideoFileIdAsync(movieId: movieId),
            Duration = duration,
        };
    }

    // Movie 129/680 are seeded by NoMercyApiFactory with real VideoFile rows,
    // which the FK on UserData.VideoFileId requires for anything that persists
    // watch progression (seek/forward/backward).
    private const int SeededMovieId = 129;
    private const int SecondSeededMovieId = 680;

    private void Cleanup(Guid userId)
    {
        _factory.Services.GetRequiredService<VideoPlaybackService>().RemoveTimer(userId: userId);
        _factory.Services.GetRequiredService<VideoPlayerStateManager>().RemoveState(userId: userId);
    }

    // =========================================================================
    // play / pause
    // =========================================================================

    [Fact]
    public async Task Play_SetsPlayStateTrue()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { PlayState = false };

        await CreateHandler().HandleCommand(user: user, command: "play", data: null, state: state, device: null);

        state.PlayState.Should().BeTrue();
        Cleanup(userId: user.Id);
    }

    [Fact]
    public async Task Pause_SetsPlayStateFalse()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { PlayState = true };

        await CreateHandler().HandleCommand(user: user, command: "pause", data: null, state: state, device: null);

        state.PlayState.Should().BeFalse();
        Cleanup(userId: user.Id);
    }

    // =========================================================================
    // seek / forward / backward
    // =========================================================================

    [Fact]
    public async Task Seek_ParsesSecondsAndSetsAbsoluteTimeInMilliseconds()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId) };

        await CreateHandler().HandleCommand(user: user, command: "seek", data: "42", state: state, device: null);

        state.Time.Should().Be(expected: 42_000);
    }

    [Fact]
    public async Task Seek_UnparsableData_LeavesTimeUnchanged()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Time = 5_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "seek", data: "not-a-number", state: state, device: null);

        state.Time.Should().Be(expected: 5_000);
    }

    [Fact]
    public async Task Forward_DefaultsTenSeconds_WhenDataIsNull()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Time = 1_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "forward", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 1_000 + 10_000);
    }

    [Fact]
    public async Task Backward_TimeBelowThreshold_ClampsToZero()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Time = 5,
        };

        await CreateHandler().HandleCommand(user: user, command: "backward", data: "10", state: state, device: null);

        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Backward_AboveThreshold_SubtractsRequestedSeconds()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Time = 30_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "backward", data: "5", state: state, device: null);

        state.Time.Should().Be(expected: 30_000 - 5_000);
    }

    // =========================================================================
    // next / previous
    // =========================================================================

    [Fact]
    public async Task Next_MidPlaylist_AdvancesToNextItemAndResetsTime()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlaylistResponseDto second = await BuildMovieItemAsync(movieId: SecondSeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = first,
            Playlist = [first, second],
            Time = 50_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "next", data: null, state: state, device: null);

        state.CurrentItem.Should().Be(expected: second);
        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Next_LastItem_CompletesPlaylist()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto only = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = only,
            Playlist = [only],
            PlayState = true,
            Time = 50_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "next", data: null, state: state, device: null);

        state.CurrentItem.Should().BeNull();
        state.PlayState.Should().BeFalse();
        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Next_NoCurrentItem_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = null, Time = 1_000 };

        await CreateHandler().HandleCommand(user: user, command: "next", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 1_000);
    }

    [Fact]
    public async Task Previous_TimeAtLeast3Seconds_RestartsCurrentItem()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlaylistResponseDto second = await BuildMovieItemAsync(movieId: SecondSeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = second,
            Playlist = [first, second],
            Time = 3_500,
        };

        await CreateHandler().HandleCommand(user: user, command: "previous", data: null, state: state, device: null);

        state.CurrentItem.Should().Be(expected: second);
        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Previous_EarlyInTrack_MovesToPriorPlaylistItem()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlaylistResponseDto second = await BuildMovieItemAsync(movieId: SecondSeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = second,
            Playlist = [first, second],
            Time = 1_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "previous", data: null, state: state, device: null);

        state.CurrentItem.Should().Be(expected: first);
        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Previous_AlreadyFirstItem_IsNoOp()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = first,
            Playlist = [first],
            Time = 1_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "previous", data: null, state: state, device: null);

        state.CurrentItem.Should().Be(expected: first);
        state.Time.Should().Be(expected: 1_000);
    }

    // =========================================================================
    // item
    // =========================================================================

    [Fact]
    public async Task Item_ValidIndex_SwitchesCurrentItemAndResetsTime()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlaylistResponseDto second = await BuildMovieItemAsync(movieId: SecondSeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = first,
            Playlist = [first, second],
            Time = 20_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "item", data: "1", state: state, device: null);

        state.CurrentItem.Should().Be(expected: second);
        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Item_OutOfRangeIndex_IsNoOp()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = first,
            Playlist = [first],
            Time = 20_000,
        };

        await CreateHandler().HandleCommand(user: user, command: "item", data: "99", state: state, device: null);

        state.CurrentItem.Should().Be(expected: first);
        state.Time.Should().Be(expected: 20_000);
    }

    [Fact]
    public async Task Item_NullData_IsNoOp()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto first = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlayerState state = new() { CurrentItem = first, Playlist = [first] };

        await CreateHandler().HandleCommand(user: user, command: "item", data: null, state: state, device: null);

        state.CurrentItem.Should().Be(expected: first);
    }

    // =========================================================================
    // episode
    // =========================================================================

    [Fact]
    public async Task Episode_MatchingSeasonAndEpisode_SwitchesAndStartsPlaying()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto episode1 = new()
        {
            Id = 62085,
            TmdbId = 1399,
            PlaylistType = MediaTypes.TvMediaType,
            LibraryType = MediaTypes.TvMediaType,
            Season = 1,
            Episode = 1,
            Duration = "0:20:00",
        };
        VideoPlaylistResponseDto episode2 = new()
        {
            Id = 62086,
            TmdbId = 1399,
            PlaylistType = MediaTypes.TvMediaType,
            LibraryType = MediaTypes.TvMediaType,
            Season = 1,
            Episode = 2,
            Duration = "0:20:00",
        };
        VideoPlayerState state = new()
        {
            CurrentItem = episode1,
            Playlist = [episode1, episode2],
            PlayState = false,
            Time = 5_000,
        };

        await CreateHandler()
            .HandleCommand(user: user, command: "episode", data: """{"season":1,"episode":2}""", state: state, device: null);

        state.CurrentItem.Should().Be(expected: episode2);
        state.Time.Should().Be(expected: 0);
        state.PlayState.Should().BeTrue();
    }

    [Fact]
    public async Task Episode_NoMatch_IsNoOp()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto episode1 = new()
        {
            Id = 62085,
            TmdbId = 1399,
            PlaylistType = MediaTypes.TvMediaType,
            Season = 1,
            Episode = 1,
        };
        VideoPlayerState state = new()
        {
            CurrentItem = episode1,
            Playlist = [episode1],
            Time = 5_000,
        };

        await CreateHandler()
            .HandleCommand(user: user, command: "episode", data: """{"season":9,"episode":9}""", state: state, device: null);

        state.CurrentItem.Should().Be(expected: episode1);
        state.Time.Should().Be(expected: 5_000);
    }

    [Fact]
    public async Task Episode_ZeroSeasonOrEpisode_IsTreatedAsMissingAndIsNoOp()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto episode1 = new()
        {
            Id = 62085,
            TmdbId = 1399,
            PlaylistType = MediaTypes.TvMediaType,
            Season = 1,
            Episode = 1,
        };
        VideoPlayerState state = new() { CurrentItem = episode1, Playlist = [episode1] };

        await CreateHandler()
            .HandleCommand(user: user, command: "episode", data: """{"season":0,"episode":0}""", state: state, device: null);

        state.CurrentItem.Should().Be(expected: episode1);
    }

    // =========================================================================
    // nextChapter / previousChapter
    // =========================================================================

    private static VideoPlayerState BuildChapteredState(int timeMs)
    {
        VideoPlaylistResponseDto item = new()
        {
            Id = SeededMovieId,
            TmdbId = SeededMovieId,
            PlaylistType = MediaTypes.MovieMediaType,
        };
        return new()
        {
            CurrentItem = item,
            Time = timeMs,
            Chapters =
            [
                new()
                {
                    Id = 1,
                    StartTime = 0,
                    EndTime = 10_000,
                    Title = "Chapter 1",
                },
                new()
                {
                    Id = 2,
                    StartTime = 10_000,
                    EndTime = 20_000,
                    Title = "Chapter 2",
                },
                new()
                {
                    Id = 3,
                    StartTime = 20_000,
                    EndTime = 30_000,
                    Title = "Chapter 3",
                },
            ],
        };
    }

    [Fact]
    public async Task NextChapter_WithinCurrentChapter_JumpsToNextChapterStart()
    {
        User user = SeededUser();
        VideoPlayerState state = BuildChapteredState(timeMs: 5_000);

        await CreateHandler().HandleCommand(user: user, command: "nextChapter", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 10_000);
    }

    [Fact]
    public async Task NextChapter_OnLastChapter_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = BuildChapteredState(timeMs: 25_000);

        await CreateHandler().HandleCommand(user: user, command: "nextChapter", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 25_000);
    }

    [Fact]
    public async Task PreviousChapter_FarIntoChapter_RestartsCurrentChapter()
    {
        User user = SeededUser();
        VideoPlayerState state = BuildChapteredState(timeMs: 15_000);

        await CreateHandler().HandleCommand(user: user, command: "previousChapter", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 10_000);
    }

    [Fact]
    public async Task PreviousChapter_NearChapterStart_JumpsToPriorChapterStart()
    {
        User user = SeededUser();
        VideoPlayerState state = BuildChapteredState(timeMs: 11_000);

        await CreateHandler().HandleCommand(user: user, command: "previousChapter", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 0);
    }

    [Fact]
    public async Task PreviousChapter_NoCurrentChapterMatch_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = BuildChapteredState(timeMs: 99_000);

        await CreateHandler().HandleCommand(user: user, command: "previousChapter", data: null, state: state, device: null);

        state.Time.Should().Be(expected: 99_000);
    }

    // =========================================================================
    // stop / mute
    // =========================================================================

    [Fact]
    public async Task Stop_ResetsStateAndDisallowsEverything()
    {
        User user = SeededUser();
        VideoPlaylistResponseDto item = await BuildMovieItemAsync(movieId: SeededMovieId);
        VideoPlayerState state = new()
        {
            CurrentItem = item,
            Playlist = [item],
            PlayState = true,
            Time = 40_000,
            DeviceId = "tv-1",
        };

        await CreateHandler().HandleCommand(user: user, command: "stop", data: null, state: state, device: null);

        state.CurrentItem.Should().BeNull();
        state.PlayState.Should().BeFalse();
        state.Time.Should().Be(expected: 0);
        state.Playlist.Should().BeEmpty();
        state.DeviceId.Should().BeNull();
        state.Actions.Disallows.Stopping.Should().BeTrue();
        state.Actions.Disallows.Seeking.Should().BeTrue();
    }

    [Fact]
    public async Task Mute_TogglesMutedState()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { Muted = false };

        await CreateHandler().HandleCommand(user: user, command: "mute", data: null, state: state, device: null);
        state.Muted.Should().BeTrue();

        await CreateHandler().HandleCommand(user: user, command: "mute", data: null, state: state, device: null);
        state.Muted.Should().BeFalse();
    }

    // =========================================================================
    // volume
    // =========================================================================

    [Fact]
    public async Task Volume_ClampsAboveMaxTo100_AndUnmutes()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Muted = true,
        };

        await CreateHandler().HandleCommand(user: user, command: "volume", data: "150", state: state, device: null);

        state.VolumePercentage.Should().Be(expected: 100);
        state.Muted.Should().BeFalse();
    }

    [Fact]
    public async Task Volume_ClampsBelowZeroTo0()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId) };

        await CreateHandler().HandleCommand(user: user, command: "volume", data: "-20", state: state, device: null);

        state.VolumePercentage.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Volume_WithConnectedDevice_PersistsVolumeOnDeviceRow()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId) };
        Client device = new()
        {
            Id = Ulid.NewUlid(),
            Sub = user.Id,
            DeviceId = $"volume-device-{Guid.NewGuid()}",
            Endpoint = "/videoHub",
            Type = "web",
            Socket = Mock.Of<ISingleClientProxy>(),
        };

        await CreateHandler().HandleCommand(user: user, command: "volume", data: "55", state: state, device: device);

        state.VolumePercentage.Should().Be(expected: 55);
        device.VolumePercent.Should().Be(expected: 55);
    }

    [Fact]
    public async Task Volume_NullCurrentItem_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = null, VolumePercentage = 30 };

        await CreateHandler().HandleCommand(user: user, command: "volume", data: "80", state: state, device: null);

        state.VolumePercentage.Should().Be(expected: 30);
    }

    // =========================================================================
    // audio / cycleAudio
    // =========================================================================

    private static List<IAudio> ThreeAudioTracks() =>
        [new() { Language = "en" }, new() { Language = "ja" }, new() { Language = "fr" }];

    [Fact]
    public async Task Audio_ValidIndex_SelectsTrack()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Audio = ThreeAudioTracks(),
        };

        await CreateHandler().HandleCommand(user: user, command: "audio", data: "1", state: state, device: null);

        state.CurrentAudio!.Language.Should().Be(expected: "ja");
    }

    [Fact]
    public async Task Audio_NegativeIndex_ClearsSelection()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Audio = ThreeAudioTracks(),
            CurrentAudio = ThreeAudioTracks()[index: 0],
        };

        await CreateHandler().HandleCommand(user: user, command: "audio", data: "-1", state: state, device: null);

        state.CurrentAudio.Should().BeNull();
    }

    [Fact]
    public async Task CycleAudio_FromLastTrack_WrapsToFirst()
    {
        User user = SeededUser();
        List<IAudio> tracks = ThreeAudioTracks();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Audio = tracks,
            CurrentAudio = tracks[^1],
        };

        await CreateHandler().HandleCommand(user: user, command: "cycleAudio", data: null, state: state, device: null);

        state.CurrentAudio.Should().Be(expected: tracks[index: 0]);
    }

    [Fact]
    public async Task CycleAudio_NoCurrentSelection_MovesToNextAfterImplicitStart()
    {
        User user = SeededUser();
        List<IAudio> tracks = ThreeAudioTracks();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Audio = tracks,
            CurrentAudio = null,
        };

        await CreateHandler().HandleCommand(user: user, command: "cycleAudio", data: null, state: state, device: null);

        state.CurrentAudio.Should().Be(expected: tracks[index: 0]);
    }

    [Fact]
    public async Task CycleAudio_NoCurrentItem_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = null, Audio = ThreeAudioTracks() };

        await CreateHandler().HandleCommand(user: user, command: "cycleAudio", data: null, state: state, device: null);

        state.CurrentAudio.Should().BeNull();
    }

    // =========================================================================
    // caption / cycleCaption
    // =========================================================================

    private static List<ISubtitle> TwoCaptionTracks() =>
        [new() { Language = "en", Type = "full" }, new() { Language = "nl", Type = "full" }];

    [Fact]
    public async Task Caption_ValidIndex_SelectsTrack()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Captions = TwoCaptionTracks(),
        };

        await CreateHandler().HandleCommand(user: user, command: "caption", data: "1", state: state, device: null);

        state.CurrentCaption!.Language.Should().Be(expected: "nl");
    }

    [Fact]
    public async Task Caption_NegativeIndex_ClearsSelection()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Captions = TwoCaptionTracks(),
            CurrentCaption = TwoCaptionTracks()[index: 0],
        };

        await CreateHandler().HandleCommand(user: user, command: "caption", data: "-1", state: state, device: null);

        state.CurrentCaption.Should().BeNull();
    }

    [Fact]
    public async Task Caption_NullData_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId) };

        await CreateHandler().HandleCommand(user: user, command: "caption", data: null, state: state, device: null);

        state.CurrentCaption.Should().BeNull();
    }

    [Fact]
    public async Task CycleCaption_FromLastTrack_ClearsToNone()
    {
        User user = SeededUser();
        List<ISubtitle> tracks = TwoCaptionTracks();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Captions = tracks,
            CurrentCaption = tracks[^1],
        };

        await CreateHandler().HandleCommand(user: user, command: "cycleCaption", data: null, state: state, device: null);

        state.CurrentCaption.Should().BeNull();
    }

    [Fact]
    public async Task CycleCaption_NoCurrentSelection_MovesToFirstTrack()
    {
        User user = SeededUser();
        List<ISubtitle> tracks = TwoCaptionTracks();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Captions = tracks,
            CurrentCaption = null,
        };

        await CreateHandler().HandleCommand(user: user, command: "cycleCaption", data: null, state: state, device: null);

        state.CurrentCaption.Should().Be(expected: tracks[index: 0]);
    }

    [Fact]
    public async Task CycleCaption_MiddleTrack_AdvancesToNext()
    {
        User user = SeededUser();
        List<ISubtitle> tracks =
        [
            new() { Language = "en" },
            new() { Language = "nl" },
            new() { Language = "de" },
        ];
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Captions = tracks,
            CurrentCaption = tracks[index: 0],
        };

        await CreateHandler().HandleCommand(user: user, command: "cycleCaption", data: null, state: state, device: null);

        state.CurrentCaption.Should().Be(expected: tracks[index: 1]);
    }

    [Fact]
    public async Task CycleCaption_NoCurrentItem_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = null, Captions = TwoCaptionTracks() };

        await CreateHandler().HandleCommand(user: user, command: "cycleCaption", data: null, state: state, device: null);

        state.CurrentCaption.Should().BeNull();
    }

    // =========================================================================
    // quality
    // =========================================================================

    private static List<IVideo> TwoQualities() => [new() { Width = 1920 }, new() { Width = 1280 }];

    [Fact]
    public async Task Quality_ValidIndex_SelectsQuality()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Qualities = TwoQualities(),
        };

        await CreateHandler().HandleCommand(user: user, command: "quality", data: "1", state: state, device: null);

        state.CurrentQuality!.Width.Should().Be(expected: 1280);
    }

    [Fact]
    public async Task Quality_NegativeIndex_ClearsSelection()
    {
        User user = SeededUser();
        VideoPlayerState state = new()
        {
            CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId),
            Qualities = TwoQualities(),
            CurrentQuality = TwoQualities()[index: 0],
        };

        await CreateHandler().HandleCommand(user: user, command: "quality", data: "-1", state: state, device: null);

        state.CurrentQuality.Should().BeNull();
    }

    [Fact]
    public async Task Quality_NullData_IsNoOp()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { CurrentItem = await BuildMovieItemAsync(movieId: SeededMovieId) };

        await CreateHandler().HandleCommand(user: user, command: "quality", data: null, state: state, device: null);

        state.CurrentQuality.Should().BeNull();
    }

    // =========================================================================
    // unknown command
    // =========================================================================

    [Fact]
    public async Task UnknownCommand_DoesNotThrow_AndLeavesStateUntouched()
    {
        User user = SeededUser();
        VideoPlayerState state = new() { PlayState = true, Time = 12_345 };

        Func<Task> act = async () =>
            await CreateHandler().HandleCommand(user: user, command: "not-a-real-command", data: null, state: state, device: null);

        await act.Should().NotThrowAsync();
        state.PlayState.Should().BeTrue();
        state.Time.Should().Be(expected: 12_345);
    }

    [Fact]
    public async Task NullOrEmptyCommand_FallsThroughToUnknownBranch_DoesNotThrow()
    {
        User user = SeededUser();
        VideoPlayerState state = new();

        Func<Task> act = async () =>
            await CreateHandler().HandleCommand(user: user, command: "", data: null, state: state, device: null);

        await act.Should().NotThrowAsync();
    }
}
