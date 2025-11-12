using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Data.Repositories;

public class MusicRepository
{
    private readonly MediaContext _mediaContext;
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    public MusicRepository(MediaContext mediaContext)
    {
        _mediaContext = mediaContext;
    }

    #region Artist Queries

    public readonly Func<MediaContext, Guid, Guid, Task<Artist?>> GetArtist =
        (MediaContext mediaContext, Guid userId, Guid id) =>
            mediaContext.Artists.AsNoTracking()
                .Where(artist => artist.Id == id)
                .Where(artist => artist.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(artist => artist.Library)
                .Include(artist => artist.ArtistUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Include(artist => artist.Translations)
                .Include(artist => artist.Images)
                .Include(artist => artist.AlbumArtist
                    .OrderBy(albumArtist => albumArtist.Album.Year)
                    // .Where(artistTrack => artistTrack.Album.AlbumUser
                    //     .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                )
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(album => album.AlbumUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(album => album.Images)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(album => album.Translations)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(album => album.AlbumMusicGenre)
                .ThenInclude(albumMusicGenre => albumMusicGenre.MusicGenre)
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(track => track.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.TrackUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Include(artist => artist.ArtistMusicGenre)
                .ThenInclude(artistMusicGenre => artistMusicGenre.MusicGenre)
                .FirstOrDefaultAsync();

    public readonly Func<MediaContext, Guid, string, IIncludableQueryable<Artist, MusicGenre>> GetArtists =
        (MediaContext mediaContext, Guid userId, string letter) =>
            mediaContext.Artists.AsNoTracking()
                .Where(artist => artist.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(artist => artist.ArtistUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Where(album => (letter == "_" || letter == "#")
                    ? Letters.Any(p => album.Name.StartsWith(p))
                    : album.Name.StartsWith(letter)
                )
                .Include(artist => artist.Translations)
                .Include(artist => artist.Images
                    .Where(image => image.Type == "background"))
                .Include(artist => artist.ArtistMusicGenre)
                .ThenInclude(artistMusicGenre => artistMusicGenre.MusicGenre);

    public async Task LikeArtistAsync(Guid userId, Artist artist, bool liked)
    {
        if (liked)
        {
            await _mediaContext.ArtistUser
                .Upsert(new(artist.Id, userId))
                .On(m => new { m.ArtistId, m.UserId })
                .WhenMatched(m => new()
                {
                    ArtistId = m.ArtistId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            ArtistUser? artistUser = await _mediaContext.ArtistUser
                .Where(au => au.ArtistId == artist.Id && au.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (artistUser is not null)
            {
                _mediaContext.ArtistUser.Remove(artistUser);
                await _mediaContext.SaveChangesAsync();
            }
        }
    }

    #endregion

    #region Album Queries

    public readonly Func<MediaContext, Guid, Guid, Task<Album?>> GetAlbum =
        (MediaContext mediaContext, Guid userId, Guid id) =>
            mediaContext.Albums.AsNoTracking()
                .Where(album => album.Id == id)
                .Where(album => album.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(album => album.Library)
                .Include(album => album.AlbumUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Include(album => album.AlbumTrack
                    .OrderBy(albumTrack => albumTrack.Track.DiscNumber)
                    .ThenBy(albumTrack => albumTrack.Track.TrackNumber)
                )
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.TrackUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .ThenInclude(artist => artist.Translations)
                .Include(album => album.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Artist)
                .ThenInclude(artist => artist.Images)
                .Include(album => album.Images)
                .Include(album => album.Translations)
                .Include(album => album.AlbumMusicGenre)
                .ThenInclude(albumMusicGenre => albumMusicGenre.MusicGenre)
                .FirstOrDefaultAsync();

    public readonly Func<MediaContext, Guid, string, IIncludableQueryable<Album, MusicGenre>> GetAlbums =
        (MediaContext mediaContext, Guid userId, string letter) =>
            mediaContext.Albums.AsNoTracking()
                .Where(artist => artist.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(artist => artist.AlbumUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )
                .Where(album => (letter == "_" || letter == "#")
                    ? Letters.Any(p => album.Name.StartsWith(p))
                    : album.Name.StartsWith(letter)
                )
                .Include(artist => artist.Translations)
                .Include(artist => artist.Images
                    .Where(image => image.Type == "background"))
                .Include(artist => artist.AlbumMusicGenre)
                .ThenInclude(artistMusicGenre => artistMusicGenre.MusicGenre);

    public async Task LikeAlbum(Guid userId, Album album, bool liked)
    {
        if (liked)
        {
            await _mediaContext.AlbumUser
                .Upsert(new(album.Id, userId))
                .On(m => new { m.AlbumId, m.UserId })
                .WhenMatched(m => new()
                {
                    AlbumId = m.AlbumId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            AlbumUser? albumUser = await _mediaContext.AlbumUser
                .Where(user => user.AlbumId == album.Id && user.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (albumUser is not null)
            {
                _mediaContext.AlbumUser.Remove(albumUser);
                await _mediaContext.SaveChangesAsync();
            }
        }
    }

    public readonly Func<MediaContext, List<Guid>, Task<List<AlbumTrack>>> GetAlbumTracksForIds =
        (MediaContext mediaContext, List<Guid> artistIds) =>
            mediaContext.AlbumTrack.AsNoTracking()
                .Where(artistTrack => artistIds.Contains(artistTrack.AlbumId))
                .Include(artistTrack => artistTrack.Track)
                .ToListAsync();

    #endregion

    #region Track Queries

    public readonly Func<MediaContext, Guid, Task<Track?>> GetTrack =
        (MediaContext mediaContext, Guid id) =>
            mediaContext.Tracks.AsNoTracking()
                .Where(x => x.Id == id)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(album => album.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Artist)
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .FirstOrDefaultAsync();

    public readonly Func<MediaContext, Guid, IEnumerable<TrackUser>> GetTracks =
        (MediaContext mediaContext, Guid userId) =>
            mediaContext.TrackUser.AsNoTracking()
                .Where(u => u.UserId.Equals(userId))
                .Include(trackUser => trackUser.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(trackUser => trackUser.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .AsEnumerable();

    public async Task LikeTrackAsync(Guid userId, Track track, bool liked)
    {
        if (liked)
        {
            await _mediaContext.TrackUser
                .Upsert(new(track.Id, userId))
                .On(m => new { m.TrackId, m.UserId })
                .WhenMatched(m => new()
                {
                    TrackId = m.TrackId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else
        {
            TrackUser? trackUser = await _mediaContext.TrackUser
                .Where(tu => tu.TrackId == track.Id && tu.UserId.Equals(userId))
                .FirstOrDefaultAsync();

            if (trackUser is not null)
            {
                _mediaContext.TrackUser.Remove(trackUser);
                await _mediaContext.SaveChangesAsync();
            }
        }
    }

    public async Task RecordPlaybackAsync(Guid trackId, Guid userId)
    {
        await _mediaContext.MusicPlays.AddAsync(new(userId, trackId));
        await _mediaContext.SaveChangesAsync();
    }

    public readonly Func<MediaContext, Guid, Task<Track?>> GetTrackWithIncludes =
        (MediaContext mediaContext, Guid id) =>
            mediaContext.Tracks.AsNoTracking()
                .Where(track => track.Id == id)
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .FirstOrDefaultAsync();

    public async Task<Lyric[]?> UpdateTrackLyricsAsync(Track track, string lyricsJson)
    {
        Track? trackEntity = await _mediaContext.Tracks
            .Where(t => t.Id == track.Id)
            .FirstOrDefaultAsync();

        if (trackEntity != null)
        {
            trackEntity._lyrics = lyricsJson;
            await _mediaContext.SaveChangesAsync();
        }

        return lyricsJson.FromJson<Lyric[]>();
    }

    #endregion

    #region Playlist Queries

    public readonly Func<MediaContext, Guid, Task<List<CarouselResponseItemDto>>> GetPlaylists =
        (mediaContext, userId) => mediaContext.Playlists
            .Where(playlist => playlist.UserId.Equals(userId))
            .Select(playlist => new CarouselResponseItemDto(playlist))
            .Take(36)
            .ToListAsync();

    public readonly Func<MediaContext, Guid, Guid, Task<Playlist?>> GetPlaylist =
        (MediaContext mediaContext, Guid userId, Guid id) => mediaContext.Playlists
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Where(u => u.UserId.Equals(userId))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync();

    #endregion

    #region Home Page Methods

    public readonly Func<MediaContext, IOrderedQueryable<Album>> GetLatestAlbums =
        (mediaContext) =>
            mediaContext.Albums
                .Where(album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Count > 0)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .OrderByDescending(album => album.CreatedAt);

    public readonly Func<MediaContext, IOrderedQueryable<Artist>> GetLatestArtists =
        (mediaContext) => mediaContext.Artists
            .Where(artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Count > 0)
            .Include(artist => artist.Images
                .Where(image => image.Type == "thumb")
                .OrderByDescending(image => image.VoteCount)
            )
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .OrderByDescending(artist => artist.CreatedAt);

    public readonly Func<MediaContext, Guid, IQueryable<ArtistTrack>> GetFavoriteArtist =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .ThenInclude(musicPlay => musicPlay.Images
                .Where(image => image.Type == "thumb")
                .OrderByDescending(image => image.VoteCount)
            )
            .SelectMany(p => p.Track.ArtistTrack);

    public readonly Func<MediaContext, Guid, IQueryable<AlbumTrack>> GetFavoriteAlbum =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(artistTrack => artistTrack.Album)
            .SelectMany(p => p.Track.AlbumTrack);

    public readonly Func<MediaContext, Guid, IQueryable<PlaylistTrack>> GetFavoritePlaylist =
        (mediaContext, userId) => mediaContext.MusicPlays
            .Where(musicPlay => musicPlay.UserId.Equals(userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.PlaylistTrack)
            .ThenInclude(artistTrack => artistTrack.Playlist)
            .SelectMany(p => p.Track.PlaylistTrack);

    public readonly Func<MediaContext, Guid, IIncludableQueryable<ArtistUser, IOrderedEnumerable<Image>>>
        GetFavoriteArtists =
            (mediaContext, userId) => mediaContext.ArtistUser
                .Where(artistUser => artistUser.UserId.Equals(userId))
                .Include(albumUser => albumUser.Artist)
                .ThenInclude(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(albumUser => albumUser.Artist)
                .ThenInclude(artist => artist.Images
                    .Where(image => image.Type == "thumb")
                    .OrderByDescending(image => image.VoteCount)
                );

    public readonly Func<MediaContext, Guid, IIncludableQueryable<AlbumUser, Track>> GetFavoriteAlbums =
        (mediaContext, userId) => mediaContext.AlbumUser
            .Where(albumUser => albumUser.UserId.Equals(userId))
            .Include(albumUser => albumUser.Album)
            .ThenInclude(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track);

    #endregion

    #region Collection Operations (for CollectionsController)

    public readonly Func<MediaContext, Guid, IEnumerable<TrackUser>> GetFavoriteTracks =
        (MediaContext mediaContext, Guid userId) =>
            mediaContext.TrackUser.AsNoTracking()
                .Where(trackUser => trackUser.UserId.Equals(userId))
                .Include(trackUser => trackUser.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(trackUser => trackUser.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .AsEnumerable();

    public readonly Func<MediaContext, List<Guid>, Task<List<ArtistTrack>>> GetArtistTracksForCollection =
        (MediaContext mediaContext, List<Guid> artistIds) =>
            mediaContext.ArtistTrack.AsNoTracking()
                .Where(artistTrack => artistIds.Contains(artistTrack.ArtistId))
                .Include(artistTrack => artistTrack.Track)
                .ToListAsync();

    #endregion

    #region Search Operations

    public readonly Func<MediaContext, string, List<Guid>> SearchArtistIds =
        (MediaContext mediaContext, string normalizedQuery) =>
            mediaContext.Artists.AsNoTracking()
                .Select(artist => new { artist.Id, artist.Name })
                .ToList()
                .Where(artist => artist.Name.NormalizeSearch().Contains(normalizedQuery))
                .Select(artist => artist.Id)
                .ToList();

    public readonly Func<MediaContext, string, List<Guid>> SearchAlbumIds =
        (MediaContext mediaContext, string normalizedQuery) =>
            mediaContext.Albums.AsNoTracking()
                .Select(album => new { album.Id, album.Name })
                .ToList()
                .Where(album => album.Name.NormalizeSearch().Contains(normalizedQuery))
                .Select(album => album.Id)
                .ToList();

    public readonly Func<MediaContext, string, List<Guid>> SearchPlaylistIds =
        (MediaContext mediaContext, string normalizedQuery) =>
            mediaContext.Playlists.AsNoTracking()
                .Select(playlist => new { playlist.Id, playlist.Name })
                .ToList()
                .Where(playlist => playlist.Name.NormalizeSearch().Contains(normalizedQuery))
                .Select(playlist => playlist.Id)
                .ToList();

    public readonly Func<MediaContext, string, List<Guid>> SearchTrackIds =
        (MediaContext mediaContext, string normalizedQuery) =>
            mediaContext.Tracks.AsNoTracking()
                .Select(track => new { track.Id, track.Name })
                .ToList()
                .Where(track => track.Name.NormalizeSearch().Contains(normalizedQuery))
                .Select(track => track.Id)
                .ToList();

    public readonly Func<MediaContext, List<Guid>, Task<List<Artist>>> GetArtistsByIds =
        (MediaContext mediaContext, List<Guid> artistIds) =>
            mediaContext.Artists.AsNoTracking()
                .Where(artist => artistIds.Contains(artist.Id))
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ToListAsync();

    public readonly Func<MediaContext, List<Guid>, Task<List<Album>>> GetAlbumsByIds =
        (MediaContext mediaContext, List<Guid> albumIds) =>
            mediaContext.Albums.AsNoTracking()
                .Where(album => albumIds.Contains(album.Id))
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(album => album.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync();

    public readonly Func<MediaContext, List<Guid>, Task<List<Playlist>>> GetPlaylistsByIds =
        (MediaContext mediaContext, List<Guid> playlistIds) =>
            mediaContext.Playlists.AsNoTracking()
                .Where(playlist => playlistIds.Contains(playlist.Id))
                .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(song => song.TrackUser)
                .ToListAsync();

    public readonly Func<MediaContext, List<Guid>, Task<List<Track>>> GetTracksByIds =
        (MediaContext mediaContext, List<Guid> trackIds) =>
            mediaContext.Tracks.AsNoTracking()
                .Where(track => trackIds.Contains(track.Id))
                .Include(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .Include(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .Include(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Playlist)
                .Include(track => track.TrackUser)
                .ToListAsync();

    #endregion

    #region Playlist Management

    public readonly Func<MediaContext, Guid, Guid, Task<PlaylistTrack?>> GetPlaylistTrack =
        (MediaContext mediaContext, Guid listId, Guid trackId) =>
            mediaContext.PlaylistTrack
                .Include(playlistTrack => playlistTrack.Track)
                .ThenInclude(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(track => track.PlaylistTrack)
                .ThenInclude(playlistTrack => playlistTrack.Track)
                .ThenInclude(track => track.Images)
                .FirstOrDefaultAsync(playlistTrack =>
                    playlistTrack.PlaylistId == listId && playlistTrack.TrackId == trackId);

    public readonly Func<MediaContext, Guid, Guid, Task<AlbumTrack?>> GetAlbumTrack =
        (MediaContext mediaContext, Guid listId, Guid trackId) =>
            mediaContext.AlbumTrack
                .Include(x => x.Track)
                .ThenInclude(track => track.AlbumTrack
                    .Where(albumTrack => albumTrack.AlbumId == listId))
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(album => album.AlbumTrack
                    .Where(albumTrack => albumTrack.AlbumId == listId))
                .ThenInclude(albumTrack => albumTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .ThenInclude(artist => artist.Images)
                .FirstOrDefaultAsync(albumTrack =>
                    albumTrack.AlbumId == listId && albumTrack.TrackId == trackId);

    public readonly Func<MediaContext, Guid, Guid, Task<ArtistTrack?>> GetArtistTrack =
        (MediaContext mediaContext, Guid listId, Guid trackId) =>
            mediaContext.ArtistTrack
                .Include(x => x.Track)
                .ThenInclude(track => track.ArtistTrack
                    .Where(artistTrack => artistTrack.ArtistId == listId))
                .ThenInclude(artistTrack => artistTrack.Artist)
                .ThenInclude(artist => artist.ArtistTrack
                    .Where(artistTrack => artistTrack.ArtistId == listId))
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(artist => artist.Translations)
                .Include(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Artist)
                .ThenInclude(artist => artist.Images)
                .FirstOrDefaultAsync(artistTrack =>
                    artistTrack.ArtistId == listId && artistTrack.TrackId == trackId);

    #endregion
}