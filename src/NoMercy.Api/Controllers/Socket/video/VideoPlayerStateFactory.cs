using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.Socket.video;

public class VideoPlayerStateFactory
{
    public static async Task<VideoPlayerState> Create(
        User user,
        Device device,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist,
        string type,
        dynamic listId)
    {
        await using MediaContext context = new();
        
        string id = listId.ToString();

        User userPreference = await context.Users
            .Include(u => u.PlaybackPreferences)
            .ThenInclude(playbackPreference => playbackPreference.Library)
            .ThenInclude(library => library.LibraryTvs
                .Where(t => type == "tv" && t.TvId == int.Parse(id)) )
            .Include(u => u.PlaybackPreferences)
            .ThenInclude(playbackPreference => playbackPreference.Library)
            .ThenInclude(library => library.LibraryMovies
                .Where(m => type == "movie" && m.MovieId == int.Parse(id)))
            .FirstAsync(u => u.Id == user.Id);

        PlaybackPreference? playbackPreference = userPreference.PlaybackPreferences
            .FirstOrDefault(p => 
                (p.MovieId is not null && p.MovieId.ToString() == id && "movie" == type) ||
                (p.TvId is not null && p.TvId.ToString() == id && "tv" == type) ||
                (p.CollectionId is not null && p.CollectionId.ToString() == id && "collection" == type) ||
                (p.SpecialId is not null && p.SpecialId.ToString() == id && "special" == type));

        if (playbackPreference is null)
        {
            playbackPreference = userPreference.PlaybackPreferences
                .FirstOrDefault(p => p.Library != null && (p.Library.Type == type ||
                                               (type == "tv" && p.Library.LibraryTvs.Any(t => t.TvId == int.Parse(id))) ||
                                               (type == "movie" && p.Library.LibraryMovies.Any(m => m.MovieId == int.Parse(id)))));
        }
        
        if (playbackPreference is null)
        {
            int width = item.Qualities.Select(q => q.Width).FirstOrDefault();
            string? audioLanguage = item.Audio.Select(a => a.Language).FirstOrDefault();
            string? subtitleLanguage = item.Captions.FirstOrDefault()?.Language;
            string? subtitleType = item.Captions.FirstOrDefault()?.Type;
            string? subtitleCodec = item.Captions.FirstOrDefault()?.Codec;
            
            playbackPreference = new()
            {
                Video = new()
                {
                    Width = width
                },
                Audio = audioLanguage is not null 
                    ? new() {
                        Language = audioLanguage
                    } 
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
            Time = item.Progress?.Time * 1000 ?? 0,
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
}