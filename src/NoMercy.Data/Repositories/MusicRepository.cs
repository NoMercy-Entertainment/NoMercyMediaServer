using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Data.Repositories;

public class MusicRepository(MediaContext mediaContext)
{
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    #region Artist Queries

    public Task<Artist?> GetArtistAsync(Guid userId, Guid id)
    {
        MediaContext context = new();
        return context.Artists
            .AsNoTracking()
            .Where(artist => artist.Id == id)
            .Where(artist => artist.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(artist => artist.Library)
            .Include(artist => artist.ArtistUser.Where(au => au.UserId == userId))
            .Include(artist => artist.Translations)
            .Include(artist => artist.Images)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ThenInclude(album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ThenInclude(album => album.Images)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ThenInclude(album => album.Translations)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ThenInclude(album => album.AlbumMusicGenre)
            .ThenInclude(amg => amg.MusicGenre)
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(at => at.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(at => at.Track)
            .ThenInclude(track => track.TrackUser.Where(tu => tu.UserId == userId))
            .Include(artist => artist.ArtistMusicGenre)
            .ThenInclude(amg => amg.MusicGenre)
            .FirstOrDefaultAsync();
    }

    public IQueryable<Artist> GetArtistsAsync(Guid userId, string letter)
    {
        MediaContext context = new();
        return context.Artists
            .AsNoTracking()
            .Where(artist => artist.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(artist => (letter == "_" || letter == "#")
                ? Letters.Any(p => artist.Name.StartsWith(p))
                : artist.Name.StartsWith(letter))
            .Include(artist => artist.ArtistUser.Where(au => au.UserId == userId))
            .Include(artist => artist.Translations)
            .Include(artist => artist.Images.Where(image => image.Type == "background"))
            .Include(artist => artist.ArtistMusicGenre)
            .ThenInclude(amg => amg.MusicGenre);
    }

    public async Task LikeArtistAsync(Guid userId, Artist artist, bool liked)
    {
        if (liked)
        {
            await mediaContext.ArtistUser
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
            ArtistUser? artistUser = await mediaContext.ArtistUser
                .FirstOrDefaultAsync(au => au.ArtistId == artist.Id && au.UserId == userId);

            if (artistUser is not null)
            {
                mediaContext.ArtistUser.Remove(artistUser);
                await mediaContext.SaveChangesAsync();
            }
        }
    }

    #endregion

    #region Album Queries

    public Task<Album?> GetAlbumAsync(Guid userId, Guid id)
    {
        MediaContext context = new();
        return context.Albums
            .AsNoTracking()
            .Where(album => album.Id == id)
            .Where(album => album.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(album => album.Library)
            .Include(album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.TrackUser.Where(tu => tu.UserId == userId))
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
            .ThenInclude(amg => amg.MusicGenre)
            .FirstOrDefaultAsync();
    }

    public IQueryable<Album> GetAlbumsAsync(Guid userId, string letter)
    {
        MediaContext context = new();
        return context.Albums
            .AsNoTracking()
            .Where(album => album.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(album => (letter == "_" || letter == "#")
                ? Letters.Any(p => album.Name.StartsWith(p))
                : album.Name.StartsWith(letter))
            .Include(album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(album => album.Translations)
            .Include(album => album.Images.Where(image => image.Type == "background"))
            .Include(album => album.AlbumMusicGenre)
            .ThenInclude(amg => amg.MusicGenre);
    }

    public async Task LikeAlbumAsync(Guid userId, Album album, bool liked)
    {
        if (liked)
        {
            await mediaContext.AlbumUser
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
            AlbumUser? albumUser = await mediaContext.AlbumUser
                .FirstOrDefaultAsync(au => au.AlbumId == album.Id && au.UserId == userId);

            if (albumUser is not null)
            {
                mediaContext.AlbumUser.Remove(albumUser);
                await mediaContext.SaveChangesAsync();
            }
        }
    }

    public Task<List<AlbumTrack>> GetAlbumTracksForIdsAsync(List<Guid> albumIds)
    {
        MediaContext context = new();
        return context.AlbumTrack
            .AsNoTracking()
            .Where(at => albumIds.Contains(at.AlbumId))
            .Include(at => at.Track)
            .ToListAsync();
    }

    #endregion

    #region Track Queries

    public Task<Track?> GetTrackAsync(Guid id)
    {
        MediaContext context = new();
        return context.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .ThenInclude(album => album.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Artist)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync();
    }

    public IQueryable<TrackUser> GetTracksAsync(Guid userId)
    {
        MediaContext context = new();
        return context.TrackUser
            .AsNoTracking()
            .Where(tu => tu.UserId == userId)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist);
    }

    public async Task LikeTrackAsync(Guid userId, Track track, bool liked)
    {
        if (liked)
        {
            await mediaContext.TrackUser
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
            TrackUser? trackUser = await mediaContext.TrackUser
                .FirstOrDefaultAsync(tu => tu.TrackId == track.Id && tu.UserId == userId);

            if (trackUser is not null)
            {
                mediaContext.TrackUser.Remove(trackUser);
                await mediaContext.SaveChangesAsync();
            }
        }
    }

    public async Task RecordPlaybackAsync(Guid trackId, Guid userId)
    {
        await mediaContext.MusicPlays.AddAsync(new(userId, trackId));
        await mediaContext.SaveChangesAsync();
    }

    public Task<Track?> GetTrackWithIncludesAsync(Guid id)
    {
        MediaContext context = new();
        return context.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .FirstOrDefaultAsync();
    }

    public async Task<Lyric[]?> UpdateTrackLyricsAsync(Track track, string lyricsJson)
    {
        await mediaContext.Upsert(track)
            .On(v => new { v.Id })
            .WhenMatched(v => new()
            {
                _lyrics = lyricsJson
            })
            .RunAsync();

        return lyricsJson.FromJson<Lyric[]>();
    }

    #endregion

    #region Playlist Queries

    public Task<List<CarouselResponseItemDto>> GetCarouselPlaylistsAsync(Guid userId)
    {
        MediaContext context = new();
        return context.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .Select(playlist => new CarouselResponseItemDto(playlist))
            .Take(36)
            .ToListAsync();
    }

    public Task<Playlist?> GetPlaylistAsync(Guid userId, Guid id)
    {
        MediaContext context = new();
        return context.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.Id == id)
            .Where(playlist => playlist.UserId == userId)
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync();
    }

    #endregion

    #region Home Page Methods

    public IQueryable<Album> GetLatestAlbumsAsync()
    {
        MediaContext context = new();
        return context.Albums
            .AsNoTracking()
            .Where(album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Count > 0)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .OrderByDescending(album => album.CreatedAt);
    }

    public IQueryable<Artist> GetLatestArtistsAsync()
    {
        MediaContext context = new();
        return context.Artists
            .AsNoTracking()
            .Where(artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Count > 0)
            .Include(artist => artist.Images.Where(image => image.Type == "thumb"))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .OrderByDescending(artist => artist.CreatedAt);
    }

    public IQueryable<MusicGenre> GetLatestGenresAsync()
    {
        MediaContext context = new();
        return context.MusicGenres
            .AsNoTracking()
            .Where(genre => genre.MusicGenreTracks.Count > 0)
            .Include(genre => genre.MusicGenreTracks)
            .OrderByDescending(genre => genre.MusicGenreTracks.Count);
    }

    public IQueryable<ArtistTrack> GetFavoriteArtistAsync(Guid userId)
    {
        MediaContext context = new();
        return context.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.UserId == userId)
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .ThenInclude(artist => artist.Images.Where(image => image.Type == "thumb"))
            .SelectMany(p => p.Track.ArtistTrack);
    }

    public IQueryable<AlbumTrack> GetFavoriteAlbumAsync(Guid userId)
    {
        MediaContext context = new();
        return context.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.UserId == userId)
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .SelectMany(p => p.Track.AlbumTrack);
    }

    public IQueryable<PlaylistTrack> GetFavoritePlaylistAsync(Guid userId)
    {
        MediaContext context = new();
        return context.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.Track.PlaylistTrack.All(pt => pt.Playlist.UserId == userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .SelectMany(p => p.Track.PlaylistTrack);
    }

    public IQueryable<ArtistUser> GetFavoriteArtistsAsync(Guid userId)
    {
        MediaContext context = new();
        return context.ArtistUser
            .AsNoTracking()
            .Where(artistUser => artistUser.UserId == userId)
            .Include(artistUser => artistUser.Artist)
            .ThenInclude(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artistUser => artistUser.Artist)
            .ThenInclude(artist => artist.Images.Where(image => image.Type == "thumb"));
    }

    public IQueryable<AlbumUser> GetFavoriteAlbumsAsync(Guid userId)
    {
        MediaContext context = new();
        return context.AlbumUser
            .AsNoTracking()
            .Where(albumUser => albumUser.UserId == userId)
            .Include(albumUser => albumUser.Album)
            .ThenInclude(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track);
    }

    #endregion

    #region Collection Operations (for CollectionsController)

    public IQueryable<TrackUser> GetFavoriteTracksAsync(Guid userId)
    {
        MediaContext context = new();
        return context.TrackUser
            .AsNoTracking()
            .Where(trackUser => trackUser.UserId == userId)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album);
    }

    public Task<List<ArtistTrack>> GetArtistTracksForCollectionAsync(List<Guid> artistIds)
    {
        MediaContext context = new();
        return context.ArtistTrack
            .AsNoTracking()
            .Where(artistTrack => artistIds.Contains(artistTrack.ArtistId))
            .Include(artistTrack => artistTrack.Track)
            .ToListAsync();
    }

    #endregion

    #region Search Operations

    public List<Guid> SearchArtistIds(string normalizedQuery)
    {
        MediaContext context = new();
        return context.Artists
            .AsNoTracking()
            .Select(artist => new { artist.Id, artist.Name })
            .ToList()
            .Where(artist => artist.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(artist => artist.Id)
            .ToList();
    }

    public List<Guid> SearchAlbumIds(string normalizedQuery)
    {
        MediaContext context = new();
        return context.Albums
            .AsNoTracking()
            .Select(album => new { album.Id, album.Name })
            .ToList()
            .Where(album => album.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(album => album.Id)
            .ToList();
    }

    public List<Guid> SearchPlaylistIds(string normalizedQuery)
    {
        MediaContext context = new();
        return context.Playlists
            .AsNoTracking()
            .Select(playlist => new { playlist.Id, playlist.Name })
            .ToList()
            .Where(playlist => playlist.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(playlist => playlist.Id)
            .ToList();
    }

    public List<Guid> SearchTrackIds(string normalizedQuery)
    {
        MediaContext context = new();
        return context.Tracks
            .AsNoTracking()
            .Select(track => new { track.Id, track.Name })
            .ToList()
            .Where(track => track.Name.NormalizeSearch().Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToList();
    }

    public Task<List<Artist>> GetArtistsByIdsAsync(List<Guid> artistIds)
    {
        MediaContext context = new();
        return context.Artists
            .AsNoTracking()
            .Where(artist => artistIds.Contains(artist.Id))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToListAsync();
    }

    public Task<List<Album>> GetAlbumsByIdsAsync(List<Guid> albumIds)
    {
        MediaContext context = new();
        return context.Albums
            .AsNoTracking()
            .Where(album => albumIds.Contains(album.Id))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.TrackUser)
            .ToListAsync();
    }

    public Task<List<Playlist>> GetPlaylistsByIdsAsync(List<Guid> playlistIds)
    {
        MediaContext context = new();
        return context.Playlists
            .AsNoTracking()
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(track => track.TrackUser)
            .ToListAsync();
    }

    public Task<List<Track>> GetTracksByIdsAsync(List<Guid> trackIds)
    {
        MediaContext context = new();
        return context.Tracks
            .AsNoTracking()
            .Where(track => trackIds.Contains(track.Id))
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .Include(track => track.TrackUser)
            .ToListAsync();
    }

    #endregion

    #region Playlist Management

    public Task<PlaylistTrack?> GetPlaylistTrackAsync(Guid userId, Guid playlistId, Guid trackId)
    {
        MediaContext context = new();
        return context.PlaylistTrack
            .Include(pt => pt.Track)
            .ThenInclude(track => track.Images)
            .Include(pt => pt.Playlist)
            .ThenInclude(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(pt => pt.Playlist)
            .ThenInclude(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .FirstOrDefaultAsync();
    }

    public Task<AlbumTrack?> GetAlbumTrackAsync(Guid userId, Guid albumId, Guid trackId)
    {
        MediaContext context = new();
        return context.AlbumTrack
            .Where(at => at.AlbumId == albumId && at.TrackId == trackId)
            .Include(at => at.Track)
            .Include(at => at.Album)
            .ThenInclude(album => album.AlbumTrack
                .OrderBy(albumTrack => albumTrack.Track.DiscNumber)
                .ThenBy(albumTrack => albumTrack.Track.TrackNumber))
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync();
    }

    public Task<ArtistTrack?> GetArtistTrackAsync(Guid userId, Guid artistId, Guid trackId)
    {
        MediaContext context = new();
        return context.ArtistTrack
            .Where(at => at.ArtistId == artistId && at.TrackId == trackId)
            .Include(at => at.Track)
            .Include(at => at.Artist)
            .ThenInclude(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .ThenInclude(album => album.Translations)
            .Include(at => at.Artist)
            .ThenInclude(artist => artist.Images)
            .FirstOrDefaultAsync();
    }

    public Task<MusicGenreTrack?> GetGenreTrackAsync(Guid userId, Guid genreId, Guid trackId)
    {
        MediaContext context = new();
        return context.MusicGenreTrack
            .Where(genre =>
                genre.Genre.AlbumMusicGenres.Any(g => g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.Genre.ArtistMusicGenres.Any(g => g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Where(mgt => mgt.GenreId == genreId && mgt.TrackId == trackId)
            
            .Include(mgt => mgt.Track)
            
            .Include(mgt => mgt.Genre)
            .ThenInclude(genre => genre.MusicGenreTracks)
            .ThenInclude(genreTrack => genreTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            
            .Include(mgt => mgt.Genre)
            .ThenInclude(genre => genre.MusicGenreTracks)
            .ThenInclude(genreTrack => genreTrack.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            
            .Include(mgt => mgt.Genre)
            .ThenInclude(genre => genre.MusicGenreTracks)
            .ThenInclude(genreTrack => genreTrack.Track)
            .ThenInclude(track => track.TrackUser)
            
            .FirstOrDefaultAsync();
    }

    #endregion

}