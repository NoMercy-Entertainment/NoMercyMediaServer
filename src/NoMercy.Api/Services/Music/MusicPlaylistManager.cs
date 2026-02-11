using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Music;

public class MusicPlaylistManager
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public MusicPlaylistManager(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    public async Task<(PlaylistTrackDto item, List<PlaylistTrackDto> playlist)> GetPlaylist(Guid userId,
        string type, Guid listId, Guid trackId, string country)
    {
        return type.ToLower().Trim() switch
        {
            // For type="track", the track ID is in the listId parameter (second param)
            // Call format: StartPlaybackCommand("track", trackId, null/empty)
            "track" => await GetSingleTrack(userId, listId, country),
            "playlist" => await GetPlaylistTracks(userId, listId, trackId, country),
            "album" => await GetAlbumTracks(userId, listId, trackId, country),
            "artist" => await GetArtistTracks(userId, listId, trackId, country),
            "genre" => await GetGenreTracks(userId, listId, trackId, country),
            _ => throw new ArgumentException($"Invalid playlist type: '{type}'", nameof(type))
        };
    }

    public (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) SplitPlaylist(List<PlaylistTrackDto> playlist,
        Guid currentTrackId)
    {
        int index = playlist.FindIndex(p => p.Id == currentTrackId);
        if (index == -1) return ([], playlist);

        List<PlaylistTrackDto> before = playlist.GetRange(0, index);
        List<PlaylistTrackDto> after = playlist.GetRange(index + 1, playlist.Count - index - 1);

        return (before, after);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetSingleTrack(Guid userId, Guid trackId, string country)
    {
        Track? track = await _musicRepository.GetTrackAsync(trackId);

        if (track is null)
            throw new("Track not found");

        // Load TrackUser data for favorite status
        bool isFavorite = await _mediaContext.TrackUser
            .AnyAsync(tu => tu.TrackId == trackId && tu.UserId == userId);
        
        if (isFavorite && !track.TrackUser.Any(tu => tu.UserId == userId))
        {
            track.TrackUser.Add(new TrackUser { TrackId = trackId, UserId = userId });
        }

        PlaylistTrackDto trackDto = new(track, country);
        
        // Return the track with an empty playlist (no other tracks to play)
        return (trackDto, []);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetPlaylistTracks(Guid userId,
        Guid listId, Guid trackId, string country)
    {
        PlaylistTrack? playlistTrack = await _musicRepository.GetPlaylistTrackAsync(userId, listId, trackId);

        if (playlistTrack is null)
            throw new("Playlist track not found");

        List<PlaylistTrackDto> playlist = playlistTrack.Playlist.Tracks
            .Select(x => new PlaylistTrackDto(x, country))
            .ToList();

        PlaylistTrackDto item = playlist.First(p => p.Id == playlistTrack.TrackId);
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(playlist, playlistTrack.TrackId);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetAlbumTracks(Guid userId,
        Guid listId, Guid trackId, string country)
    {
        AlbumTrack? albumTrack = await _musicRepository.GetAlbumTrackAsync(userId, listId, trackId);

        if (albumTrack is null)
            throw new("Album track not found");

        List<PlaylistTrackDto> playlist = albumTrack.Album.AlbumTrack
            .Select(x => new PlaylistTrackDto(x, country))
            .OrderBy(x => x.Disc)
            .ThenBy(x => x.Track)
            .ToList();

        PlaylistTrackDto item = playlist.First(p => p.Id == albumTrack.TrackId);
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(playlist, albumTrack.TrackId);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetArtistTracks(Guid userId,
        Guid listId, Guid trackId, string country)
    {
        ArtistTrack? artistTrack = await _musicRepository.GetArtistTrackAsync(userId, listId, trackId);

        if (artistTrack is null)
            throw new("Artist track not found");

        List<PlaylistTrackDto> playlist = artistTrack.Artist.ArtistTrack
            .Select(x => new PlaylistTrackDto(x, country))
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.AlbumName)
            .ThenBy(x => x.Disc)
            .ThenBy(x => x.Track)
            .ToList();

        PlaylistTrackDto item = playlist.First(p => p.Id == artistTrack.TrackId);
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(playlist, artistTrack.TrackId);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetGenreTracks(Guid userId,
        Guid listId, Guid trackId, string country)
    {
        MusicGenreTrack? genreTrack = await _musicRepository.GetGenreTrackAsync(userId, listId, trackId);

        if (genreTrack is null)
            throw new("Genre track not found");

        List<PlaylistTrackDto> playlist = genreTrack.Genre.MusicGenreTracks
            .Select(x => new PlaylistTrackDto(x, country))
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Disc)
            .ThenBy(x => x.Track)
            .ToList();

        PlaylistTrackDto item = playlist.First(p => p.Id == genreTrack.TrackId);
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(playlist, genreTrack.TrackId);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        return (item, sortedPlaylist);
    }
}