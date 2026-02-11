
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Services.Video;

public class VideoPlaylistManager
{
    private readonly MovieRepository _movieRepository;
    private readonly TvShowRepository _tvShowRepository;
    private readonly CollectionRepository _collectionRepository;
    private readonly SpecialRepository _specialRepository;
    private readonly MediaContext _mediaContext;

    public VideoPlaylistManager(
        MediaContext mediaContext, 
        MovieRepository movieRepository, 
        CollectionRepository collectionRepository,
        SpecialRepository specialRepository,
        TvShowRepository tvShowRepository)
    {
        _movieRepository = movieRepository;
        _tvShowRepository = tvShowRepository;
        _collectionRepository = collectionRepository;
        _specialRepository = specialRepository;
        _mediaContext = mediaContext;
    }

    public async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetPlaylist(
        Guid userId, string type, dynamic listId, int? itemId, string language, string country)
    {
        return type switch
        {
            Config.SpecialMediaType => await GetSpecialItems(userId, listId, itemId, language, country),
            Config.CollectionMediaType => await GetCollectionItems(userId, listId, itemId, language, country),
            Config.TvMediaType => await GetTvItems(userId, listId, itemId, language, country),
            Config.MovieMediaType => await GetMovieItems(userId, listId, itemId, language, country),
            _ => throw new ArgumentException("Invalid playlist type", nameof(type))
        };
    }
    
    public (List<VideoPlaylistResponseDto> before, List<VideoPlaylistResponseDto> after) SplitPlaylist(List<VideoPlaylistResponseDto> playlist,
        int currentTrackId)
    {
        int index = playlist.FindIndex(p => p.Id == currentTrackId);
        if (index == -1) return ([], playlist);

        List<VideoPlaylistResponseDto> before = playlist.GetRange(0, index);
        List<VideoPlaylistResponseDto> after = playlist.GetRange(index + 1, playlist.Count - index - 1);

        return (before, after);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetSpecialItems(
        Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Special? special = await _specialRepository.GetSpecialPlaylistAsync(userId, Ulid.Parse(listId), language, country);

        List<VideoPlaylistResponseDto> playlist = special?.Items
            .OrderBy(item => item.Order)
            .Select((item, index) => item.EpisodeId is not null
                ? new(item.Episode ?? new Episode(), Config.SpecialMediaType,  listId, country, index)
                : new VideoPlaylistResponseDto(item.Movie ?? new Movie(), Config.SpecialMediaType, listId, country, index))
            .ToList() ?? [];
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetCollectionItems(
        Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Collection? collection = await _collectionRepository.GetCollectionPlaylistAsync(userId, int.Parse(listId), language, country);

        List<VideoPlaylistResponseDto> playlist = collection?.CollectionMovies
            .Select((movie, index) => new VideoPlaylistResponseDto(movie.Movie,Config.CollectionMediaType, listId, country, index + 1, collection))
            .ToList() ?? [];
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetTvItems(
        Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Tv? tv = await _tvShowRepository.GetTvPlaylistAsync(userId, int.Parse(listId), language, country);

        VideoPlaylistResponseDto[] episodes = tv?.Seasons
            .Where(season => season.SeasonNumber > 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, Config.TvMediaType, listId, country))
            .ToArray() ?? [];

        VideoPlaylistResponseDto[] extras = tv?.Seasons
            .Where(season => season.SeasonNumber == 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, Config.TvMediaType, listId, country))
            .ToArray() ?? [];
        
        List<VideoPlaylistResponseDto> playlist = episodes.Concat(extras).ToList();
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetMovieItems(
        Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        List<Movie> movies = await _movieRepository.GetMoviePlaylistAsync(userId, int.Parse(listId), language, country);
        List<VideoPlaylistResponseDto> playlist = movies
            .Select(movie => new VideoPlaylistResponseDto(movie, Config.MovieMediaType, int.Parse(listId), country))
            .ToList();
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId) 
            ?? playlist.FirstOrDefault();
        
        return (item, playlist);
    }
    
}