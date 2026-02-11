using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using static System.Int32;

namespace NoMercy.Api.Services.Video;

public class VideoPlayerStateFactory
{
    public static async Task<VideoPlayerState> Create(
        IDbContextFactory<MediaContext> contextFactory,
        User user,
        Device device,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist,
        string type,
        dynamic listId)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();

        ArgumentNullException.ThrowIfNull(listId);

        string id = listId.ToString();

        // parse id once and safely
        TryParse(id, out int parsedId);

        // Include playback preferences and their Library collections to ensure data available for matching
        User? userPreference = await context.Users
            .Include(u => u.PlaybackPreferences)
                .ThenInclude(playbackPreference => playbackPreference.Library)
                    .ThenInclude(library => library.LibraryTvs)
            .Include(u => u.PlaybackPreferences)
                .ThenInclude(playbackPreference => playbackPreference.Library)
                    .ThenInclude(library => library.LibraryMovies)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (userPreference is null)
        {
            // Fallback to default playback preference when the user could not be loaded
            return new()
            {
                DeviceId = device.DeviceId,
                VolumePercentage = device.VolumePercent,
                CurrentItem = item,
                CurrentAudio = null,
                CurrentCaption = null,
                CurrentQuality = null,
                Playlist = playlist,
                PlayState = true,
                Time = (item.Progress?.Time ?? 0) * 1000,
                Duration = item.Duration.ToMilliSeconds(),
                CurrentList = new($"/{type}/{listId}/watch", UriKind.Relative),
                Actions = new()
                {
                    Disallows = new()
                    {
                        Stopping = false,
                        Seeking = false,
                        Muting = false,
                        Pausing = false,
                        Resuming = true,
                        Previous = playlist.IndexOf(item) == 0,
                        Next = playlist.IndexOf(item) == playlist.Count - 1
                    }
                }
            };
        }

        PlaybackPreference? playbackPreference = FindPlaybackPreference(userPreference, id, parsedId, type);

        if (playbackPreference is null)
        {
            playbackPreference = CreateDefaultPlaybackPreference(item);
        }
        
        return new()
        {
            DeviceId = device.DeviceId,
            VolumePercentage = device.VolumePercent,
            CurrentItem = item,
            CurrentAudio = playbackPreference.Audio,
            CurrentCaption = playbackPreference.Subtitle,
            CurrentQuality = playbackPreference.Video,
            Playlist = playlist,
            PlayState = true,
            Time = (item.Progress?.Time ?? 0) * 1000,
            Duration = item.Duration.ToMilliSeconds(),
            CurrentList = new($"/{type}/{listId}/watch", UriKind.Relative),
            Actions = new()
            {
                Disallows = new()
                {
                    Stopping = false,
                    Seeking = false,
                    Muting = false,
                    Pausing = false,
                    Resuming = true,
                    Previous = playlist.IndexOf(item) == 0,
                    Next = playlist.IndexOf(item) == playlist.Count - 1
                }
            }
        };
    }

    private static PlaybackPreference? FindPlaybackPreference(User userPreference, string id, int parsedId, string type)
    {
        PlaybackPreference? byIds = userPreference.PlaybackPreferences
            .FirstOrDefault(p =>
                (p.MovieId is not null && p.MovieId.ToString() == id && Config.MovieMediaType == type) ||
                (p.TvId is not null && p.TvId.ToString() == id && Config.TvMediaType == type) ||
                (p.CollectionId is not null && p.CollectionId.ToString() == id && Config.CollectionMediaType == type) ||
                (p.SpecialId is not null && p.SpecialId.ToString() == id && Config.SpecialMediaType == type));

        if (byIds is not null)
            return byIds;

        return userPreference.PlaybackPreferences
            .FirstOrDefault(p => p.Library != null && (p.Library.Type == type ||
                                                      (type == Config.TvMediaType && p.Library.LibraryTvs.Any(t => t.TvId == parsedId)) ||
                                                      (type == Config.MovieMediaType && p.Library.LibraryMovies.Any(m => m.MovieId == parsedId))));
    }

    private static PlaybackPreference CreateDefaultPlaybackPreference(VideoPlaylistResponseDto item)
    {
        int? width = item.Qualities.Select(q => q.Width).FirstOrDefault();
        string? audioLanguage = item.Audio.Select(a => a.Language).FirstOrDefault();
        string? subtitleLanguage = item.Captions.FirstOrDefault()?.Language;
        string? subtitleType = item.Captions.FirstOrDefault()?.Type;
        string? subtitleCodec = item.Captions.FirstOrDefault()?.Codec;

        return new()
        {
            Video = width.HasValue
                ? new()
                {
                    Width = width.Value
                }
                : null,
            Audio = audioLanguage is not null
                ? new() { Language = audioLanguage }
                : null,
            Subtitle = subtitleLanguage is not null
                ? new()
                {
                    Language = subtitleLanguage,
                    Type = subtitleType,
                    Codec = subtitleCodec
                }
                : null
        };
    }
}