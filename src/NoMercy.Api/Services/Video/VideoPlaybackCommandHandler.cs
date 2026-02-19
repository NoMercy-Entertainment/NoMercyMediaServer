using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Api.Services.Video;

public class VideoPlaybackCommandHandler(VideoPlaybackService videoPlaybackService, IServiceScopeFactory scopeFactory)
{
    public async Task HandleCommand(User user, string command, object? data, VideoPlayerState state, Client? device)
    {
        switch (command)
        {
            case "play":
                // if(state.Actions.Disallows.Resuming) break;
                HandlePlay(user, state);
                break;
            case "pause":
                // if(state.Actions.Disallows.Pausing) break;
                HandlePause(user, state);
                break;
            case "seek":
                // if(state.Actions.Disallows.Seeking) break;
                await HandleSeek(user, state, data);
                break;
            case "item":
                // if(state.Actions.Disallows.Item) break;
                await HandleItem(state, data);
                break;
            case "episode":
                // if(state.Actions.Disallows.Item) break;
                await HandleEpisode(state, data);
                break;
            case "forward":
                // if(state.Actions.Disallows.Seeking) break;
                await HandleForward(user, state, data);
                break;
            case "backward":
                // if(state.Actions.Disallows.Seeking) break;
                await HandleBackward(user, state, data);
                break;
            case "next":
                // if(state.Actions.Disallows.Next) break;
                HandleNext(state);
                break;
            case "previous":
                // if(state.Actions.Disallows.Previous) break;
                HandlePrevious(state);
                break;
            case "nextChapter":
                // if(state.Actions.Disallows.Next) break;
                HandleNextChapter(state);
                break;
            case "previousChapter":
                // if(state.Actions.Disallows.Previous) break;
                HandlePreviousChapter(state);
                break;
            case "stop":
                // if(state.Actions.Disallows.Stopping) break;
                HandleStop(state);
                break;
            case "mute":
                // if(state.Actions.Disallows.Muting) break;
                state.Muted = !state.Muted;
                break;
            case "volume":
                // if(state.Actions.Disallows.Volume) break;
                await HandleVolume(data, state, device);
                break;
            case "audio":
                // if(state.Actions.Disallows.Audio) break;
                await HandleAudio(user, state, data);
                break;
            case "cycleAudio":
                // if(state.Actions.Disallows.Audio) break;
                await HandleCycleAudio(user, state);
                break;
            case "caption":
                // if(state.Actions.Disallows.Caption) break;
                await HandleCaption(user, state, data);
                break;
            case "cycleCaption":
                // if(state.Actions.Disallows.Caption) break;
                await HandleCycleCaption(user, state);
                break;
            case "quality":
                // if(state.Actions.Disallows.Quality) break;
                await HandleQuality(user, state, data); 
                break;
            default:
                // Handle unknown command or log it
                Console.WriteLine($"Unknown command: {command}");
                break;
        }
    }

    private void HandlePlay(User user, VideoPlayerState state)
    {
        state.PlayState = true;
        videoPlaybackService.StartPlaybackTimer(user);
    }

    private void HandlePause(User user, VideoPlayerState state)
    {
        state.PlayState = false;
        videoPlaybackService.RemoveTimer(user.Id);
    }

    private async Task HandleSeek(User user, VideoPlayerState state, object? data)
    {
        int seekTime = int.Parse(data?.ToString() ?? "0") * 1000;
        state.Time = seekTime;
        await videoPlaybackService.StoreWatchProgression(state, user);
    }
    
    private async Task HandleForward(User user, VideoPlayerState state, object? data)
    {
        int seekTime = int.Parse(data?.ToString() ?? "10") * 1000;
        state.Time += seekTime;
        await videoPlaybackService.StoreWatchProgression(state, user);
    }
    
    private async Task HandleBackward(User user, VideoPlayerState state, object? data)
    {
        if (state.Time < 10)
        {
            state.Time = 0;
            return;
        }
        
        int seekTime = int.Parse(data?.ToString() ?? "10") * 1000;
        state.Time -= seekTime;
        await videoPlaybackService.StoreWatchProgression(state, user);
    }

    private void HandleNext(VideoPlayerState state)
    {
        if (state.CurrentItem == null) return;
        
        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        if (currentIndex < state.Playlist.Count - 1)
        {
            state.CurrentItem = state.Playlist[currentIndex + 1];
            state.Time = 0;
        }
        else
        {
            HandlePlaylistCompletion(state);
        }
    }

    private void HandlePlaylistCompletion(VideoPlayerState state)
    {
        // If repeat is off, stop playback
        state.PlayState = false;
        state.Time = 0;
        state.CurrentItem = null;
    }

    private void HandlePrevious(VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;

        if (state.Time >= 3000)
        {
            state.Time = 0;
            return;
        }
        
        if(state.Playlist.IndexOf(state.CurrentItem) == 0) return;
        
        int currentIndex = state.Playlist.IndexOf(state.CurrentItem);
        if (currentIndex > 0)
        {
            state.CurrentItem = state.Playlist[currentIndex - 1];
            state.Time = 0;
        }
    }
    
    private Task HandleItem(VideoPlayerState state, object? data)
    {
        if (data is null || state.CurrentItem is null) return Task.CompletedTask;
        
        int itemId = int.Parse(data.ToString() ?? string.Empty);
        VideoPlaylistResponseDto? item = state.Playlist.ElementAtOrDefault(itemId);
        
        if (item is null) return Task.CompletedTask;

        state.CurrentItem = item;
        state.Time = 0;

        return Task.CompletedTask;
    }
    
    private class EpisodeData
    {
        [JsonProperty("season")] public int Season { get; set; }
        [JsonProperty("episode")] public int Episode { get; set; }
    }
    
    private async Task HandleEpisode(VideoPlayerState state, object? data)
    {
        if (data is null || state.CurrentItem is null) return;
        
        EpisodeData? episodeData = data.ToString().FromJson<EpisodeData>();
        if (episodeData is null || episodeData.Season == 0 || episodeData.Episode == 0) return;

        VideoPlaylistResponseDto? item = state.Playlist.FirstOrDefault(p => 
            p.PlaylistType == Config.TvMediaType && p.Season == episodeData.Season && p.Episode == episodeData.Episode);
        
        if (item is null) return;

        state.CurrentItem = item;
        state.Time = 0;
        state.PlayState = true;
    }

    private void HandleStop(VideoPlayerState state)
    {
        state.DeviceId = null;
        state.CurrentItem = null;
        state.PlayState = false;
        state.Time = 0;
        state.Playlist = [];
        state.CurrentList = new("", UriKind.Relative);
        state.Actions = new()
        {
            Disallows = new()
            {
                Previous = true,
                Next = true,
                Resuming = true,
                Pausing = true,
                Stopping = true,
                Seeking = true,
                Muting = true
            }
        };
    }
    
    private async Task HandleVolume(object? data, VideoPlayerState state, Client? device)
    {
        if (data is null || state.CurrentItem is null) return;
        
        int volume = int.Parse(data.ToString() ?? string.Empty);
        
        state.VolumePercentage = volume;
        state.Muted = false;
        
        if (device is not null)
        {
            device.VolumePercent = volume;

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
            await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync();
            await mediaContext.Devices
                .Where(d => d.DeviceId == device.DeviceId)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
        }
        
    }

    private void HandlePreviousChapter(VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;
        IChapter? currentChapter = state.CurrentItem.Chapters
            .FirstOrDefault(c => state.Time >= c.StartTime && state.Time <= c.EndTime);
        if (currentChapter is null) return;
        
        if(state.Time - 3000 > currentChapter.StartTime)
        {
            state.Time = currentChapter.StartTime;
            return;
        }
        
        int index = state.CurrentItem.Chapters.IndexOf(currentChapter);
        if (index > 0)
        {
            IChapter previousChapter = state.CurrentItem.Chapters[index - 1];
            state.Time = previousChapter.StartTime;
        }
    }
    
    private void HandleNextChapter(VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;
        
        IChapter? currentChapter = state.CurrentItem.Chapters
            .FirstOrDefault(c => state.Time >= c.StartTime && state.Time <= c.EndTime);
        if (currentChapter is null) return;
        
        int index = state.CurrentItem.Chapters.IndexOf(currentChapter);
        if (index + 1 <= state.CurrentItem.Chapters.Count - 1)
        {
            IChapter nextChapter = state.CurrentItem.Chapters[index + 1];
            state.Time = nextChapter.StartTime;
        }
    }

    private async Task HandleAudio(User user, VideoPlayerState state, object? data)
    {
        if (data is null || state.CurrentItem is null) return;
        
        int index = int.Parse(data.ToString() ?? string.Empty);
        
        if (index < 0)
        {
            state.CurrentAudio = null;
            await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
            return;
        }
        
        IAudio? audio = state.CurrentItem.Audio.ElementAtOrDefault(index);
        if (audio is not null)
        {
            state.CurrentAudio = audio;
        }
        
        await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
    }
    
    private async Task HandleCycleAudio(User user, VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;
        
        int currentIndex = state.CurrentAudio is not null 
            ? state.CurrentItem.Audio.IndexOf(state.CurrentAudio)
            : -1;
        if (currentIndex >= state.CurrentItem.Audio.Count - 1)
        {
            state.CurrentAudio = state.CurrentItem.Audio.First();
            await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
            return;
        }
        
        IAudio nextAudio = state.CurrentItem.Audio[currentIndex + 1];
        state.CurrentAudio = nextAudio;
        
        await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
    }

    private async Task HandleCaption(User user, VideoPlayerState state, object? data)
    {
        if (data is null) return;
        
        int index = int.Parse(data.ToString() ?? string.Empty);
        
        if (index < 0)
        {
            state.CurrentCaption = null;
            await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
            return;
        }

        ISubtitle? track = state.CurrentItem?.Captions.ElementAtOrDefault(index);
        if (track is not null)
        {
            state.CurrentCaption = track;
        }
        
        await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
    }
    
    private async Task HandleCycleCaption(User user, VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;
        
        int currentIndex = state.CurrentCaption is not null 
            ? state.CurrentItem.Captions.IndexOf(state.CurrentCaption)
            : -1;
        if (currentIndex >= state.CurrentItem.Captions.Count - 1)
        {
            state.CurrentCaption = null;
            await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, null);
            return;
        }
        if (currentIndex < 0)
        {
            state.CurrentCaption = state.CurrentItem.Captions.First();
            await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
            return;
        }
        
        ISubtitle nextCaption = state.CurrentItem.Captions[currentIndex + 1];
        state.CurrentCaption = nextCaption;
        
        await SetPlaybackPreference(user, state, state.CurrentAudio, state.CurrentQuality, state.CurrentCaption);
    }
    
    private async Task HandleQuality(User user, VideoPlayerState state, object? data)
    {
        if (data is null) return;
        
        int index = int.Parse(data.ToString() ?? string.Empty);
        
        if (index < 0)
        {
            state.CurrentQuality = null;
            await SetPlaybackPreference(user, state, state.CurrentAudio, null, state.CurrentCaption);
            return;
        }
        
        IVideo? video = state.CurrentItem?.Qualities.ElementAtOrDefault(index);
        if (video is not null)
        {
            state.CurrentQuality = video;
        }
        
        await SetPlaybackPreference(user, state, state.CurrentAudio, video, state.CurrentCaption);
    }
    
    private async Task UserSetLibraryPreference(MediaContext mediaContext, User user, VideoPlayerState state)
    {
        if (state.CurrentItem is null) return;
        
        bool userHasLibraryPreference = await mediaContext.Users
            .Include(u => u.PlaybackPreferences)
            .ThenInclude(playbackPreference => playbackPreference.Library)
            .Where(u => u.Id == user.Id)
            .Select(x => x.PlaybackPreferences
                .Any(p => p.Library != null &&  p.Library.Type == state.CurrentItem!.LibraryType))
            .FirstAsync();

        if (userHasLibraryPreference) return; 
        
        PlaybackPreference playbackPreference = new()
        {
            UserId = user.Id,
            Video = state.CurrentQuality?.Width is not null
                ? new()
                {
                    Width = state.CurrentQuality.Width,
                    BitRate = null,
                    FileSize = null,
                    Height = null
                }
                : null,
            Audio = state.CurrentAudio?.Language is not null 
                ? new() 
                {
                    Language = state.CurrentAudio.Language,
                    FileSize = null
                }
                : null,
            Subtitle = state.CurrentCaption?.Language is not null
                ? new()
                {
                    Language = state.CurrentCaption.Language,
                    Type = state.CurrentCaption.Type,
                    Codec = state.CurrentCaption.Codec,
                    FileSize = null
                }
                : null,
            LibraryId = mediaContext.Libraries
                .Where(l => l.Type == state.CurrentItem!.LibraryType)
                .Select(l => l.Id)
                .FirstOrDefault()
        };

        await mediaContext.PlaybackPreferences
            .Upsert(playbackPreference)
            .On(p => new { p.UserId, p.LibraryId })
            .WhenMatched((po, pi) => new()
            {
                LibraryId = pi.LibraryId,
                _audio = pi._audio,
                _video = pi._video,
                _subtitle = pi._subtitle
            })
            .RunAsync();
    }

    private async Task SetPlaybackPreference(User user, VideoPlayerState state, IAudio? audio, IVideo? video, ISubtitle? subtitle)
    {
        if (state.CurrentItem is null) return;
        
        PlaybackPreference playbackPreference = new()
        {
            UserId = user.Id,
            MovieId = state.CurrentItem.PlaylistType == Config.MovieMediaType
                ? state.CurrentItem.TmdbId
                : null,
            TvId = state.CurrentItem.PlaylistType == Config.TvMediaType
                ? state.CurrentItem.TmdbId
                : null,
            CollectionId = state.CurrentItem.PlaylistType == Config.CollectionMediaType
                ? int.Parse(state.CurrentItem.PlaylistId) 
                : null,
            SpecialId = state.CurrentItem.PlaylistType == Config.SpecialMediaType
                ? Ulid.Parse(state.CurrentItem.PlaylistId) 
                : null,
            Video = video?.Width is not null
                ? new()
                {
                    Width = video.Width,
                    BitRate = null,
                    FileSize = null,
                    Height = null
                }
                : null,
            Audio = audio?.Language is not null 
                ? new() 
                {
                    Language = audio.Language,
                    FileSize = null
                }
                : null,
            Subtitle = subtitle?.Language is not null
                ? new()
                {
                    Language = subtitle.Language,
                    Type = subtitle.Type,
                    Codec = subtitle.Codec,
                    FileSize = null
                }
                : null
        };
        
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync();

        UpsertCommandBuilder<PlaybackPreference> query = mediaContext.PlaybackPreferences
            .Upsert(playbackPreference);

        switch (state.CurrentItem.PlaylistType)
        {
            case Config.MovieMediaType:
                query.On(p => new { p.UserId, p.MovieId });
                break;
            case Config.TvMediaType:
                query.On(p => new { p.UserId, p.TvId });
                break;
            case Config.CollectionMediaType:
                query.On(p => new { p.UserId, p.CollectionId });
                break;
            case Config.SpecialMediaType:
                query.On(p => new { p.UserId, p.SpecialId });
                break;
        }
        
        await query.WhenMatched((po, pi) => new()
            {
                _audio = pi._audio,
                _video = pi._video,
                _subtitle = pi._subtitle
            })
            .RunAsync();

        await UserSetLibraryPreference(mediaContext, user, state);
    }
    
}