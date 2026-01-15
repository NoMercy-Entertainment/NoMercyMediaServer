using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicPlaylistManager
{
    private readonly MusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public MusicPlaylistManager(MusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    public async Task<(PlaylistTrackDto item, List<PlaylistTrackDto> playlist)> GetPlaylist(
        string type, Guid listId, Guid trackId, string country)
    {
        return type switch
        {
            "playlist" => await GetPlaylistTracks(listId, trackId, country),
            "album" => await GetAlbumTracks(listId, trackId, country),
            "artist" => await GetArtistTracks(listId, trackId, country),
            _ => throw new ArgumentException("Invalid playlist type", nameof(type))
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

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetPlaylistTracks(
        Guid listId, Guid trackId, string country)
    {
        PlaylistTrack? playlistTrack = await _musicRepository.GetPlaylistTrackAsync(listId, trackId);

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

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetAlbumTracks(
        Guid listId, Guid trackId, string country)
    {
        AlbumTrack? albumTrack = await _musicRepository.GetAlbumTrackAsync(listId, trackId);

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

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetArtistTracks(
        Guid listId, Guid trackId, string country)
    {
        ArtistTrack? artistTrack = await _musicRepository.GetArtistTrackAsync(listId, trackId);

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
}