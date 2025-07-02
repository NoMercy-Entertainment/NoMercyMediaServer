
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.video;

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
        Guid userId, string type, dynamic listId, int? itemId, string language)
    {
        return type switch
        {
            "specials" => await GetSpecialItems(userId, listId, itemId, language),
            "collection" => await GetCollectionItems(userId, listId, itemId, language),
            "tv" => await GetTvItems(userId, listId, itemId, language),
            "movie" => await GetMovieItems(userId, listId, itemId, language),
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
        Guid userId, dynamic listId, int? itemId, string language)
    {
        Special? special = await _specialRepository.GetSpecialPlaylist(_mediaContext, userId, Ulid.Parse(listId), language);

        List<VideoPlaylistResponseDto> playlist = special?.Items
            .OrderBy(item => item.Order)
            .Select((item, index) => item.EpisodeId is not null
                ? new(item.Episode ?? new Episode(), "specials",  listId, index)
                : new VideoPlaylistResponseDto(item.Movie ?? new Movie(), "specials",  listId, index))
            .ToList() ?? [];
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Any())
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetCollectionItems(
        Guid userId, dynamic listId, int? itemId, string language)
    {
        Collection? collection = await _collectionRepository.GetCollectionPlaylistAsync(userId, int.Parse(listId), language);

        List<VideoPlaylistResponseDto> playlist = collection?.CollectionMovies
            .Select((movie, index) => new VideoPlaylistResponseDto(movie.Movie,"collection", listId, index + 1, collection))
            .ToList() ?? [];
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Any())
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetTvItems(
        Guid userId, dynamic listId, int? itemId, string language)
    {
        Tv? tv = await _tvShowRepository.GetTvPlaylistAsync(userId, int.Parse(listId), language);

        VideoPlaylistResponseDto[] episodes = tv?.Seasons
            .Where(season => season.SeasonNumber > 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", listId))
            .ToArray() ?? [];

        VideoPlaylistResponseDto[] extras = tv?.Seasons
            .Where(season => season.SeasonNumber == 0)
            .SelectMany(season => season.Episodes)
            .Select(episode => new VideoPlaylistResponseDto(episode, "tv", listId))
            .ToArray() ?? [];
        
        List<VideoPlaylistResponseDto> playlist = episodes.Concat(extras).ToList();
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId);

        if (item is null && playlist.Any(p => p.Progress?.Date is not null))
        {
            item = playlist
                .OrderByDescending(p => p.Progress?.Date)
                .FirstOrDefault();
        }
        if (item is null && playlist.Any())
        {
            item = playlist.FirstOrDefault();
        }
        
        return (item, playlist);
    }
    
    private async Task<(VideoPlaylistResponseDto? item, List<VideoPlaylistResponseDto> playlist)> GetMovieItems(
        Guid userId, dynamic listId, int? itemId, string language)
    {
        List<Movie> movies = await _movieRepository.GetMoviePlaylistAsync(userId, int.Parse(listId), language);
        List<VideoPlaylistResponseDto> playlist = movies
            .Select(movie => new VideoPlaylistResponseDto(movie, "movie", int.Parse(listId)))
            .ToList();
        
        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(p => p.Id == itemId) 
            ?? playlist.FirstOrDefault();
        
        return (item, playlist);
    }
    
}