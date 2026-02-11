using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
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
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Data.Repositories;

public class MusicRepository(MediaContext mediaContext)
{
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    #region Artist Queries

    public Task<Artist?> GetArtistAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => artist.Id == id)
            .ForUser(userId)
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
            .FirstOrDefaultAsync(ct);
    }

    public IQueryable<Artist> GetArtists(Guid userId, string letter)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .ForUser(userId)
            .Where(artist => (letter == "_" || letter == "#")
                ? Letters.Any(p => artist.Name.StartsWith(p))
                : artist.Name.StartsWith(letter))
            .Include(artist => artist.ArtistUser.Where(au => au.UserId == userId))
            .Include(artist => artist.Translations)
            .Include(artist => artist.Images.Where(image => image.Type == "background"))
            .Include(artist => artist.ArtistMusicGenre)
            .ThenInclude(amg => amg.MusicGenre);
    }

    public async Task LikeArtistAsync(Guid userId, Artist artist, bool liked, CancellationToken ct = default)
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
                .FirstOrDefaultAsync(au => au.ArtistId == artist.Id && au.UserId == userId, ct);

            if (artistUser is not null)
            {
                mediaContext.ArtistUser.Remove(artistUser);
                await mediaContext.SaveChangesAsync(ct);
            }
        }
    }

    #endregion

    #region Album Queries

    public Task<Album?> GetAlbumAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => album.Id == id)
            .ForUser(userId)
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
            .FirstOrDefaultAsync(ct);
    }

    public IQueryable<Album> GetAlbums(Guid userId, string letter)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .ForUser(userId)
            .Where(album => (letter == "_" || letter == "#")
                ? Letters.Any(p => album.Name.StartsWith(p))
                : album.Name.StartsWith(letter))
            .Include(album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(album => album.Translations)
            .Include(album => album.Images.Where(image => image.Type == "background"))
            .Include(album => album.AlbumMusicGenre)
            .ThenInclude(amg => amg.MusicGenre);
    }

    public async Task LikeAlbumAsync(Guid userId, Album album, bool liked, CancellationToken ct = default)
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
                .FirstOrDefaultAsync(au => au.AlbumId == album.Id && au.UserId == userId, ct);

            if (albumUser is not null)
            {
                mediaContext.AlbumUser.Remove(albumUser);
                await mediaContext.SaveChangesAsync(ct);
            }
        }
    }

    public Task<List<AlbumTrack>> GetAlbumTracksForIdsAsync(List<Guid> albumIds, CancellationToken ct = default)
    {
        return mediaContext.AlbumTrack
            .AsNoTracking()
            .Where(at => albumIds.Contains(at.AlbumId))
            .Include(at => at.Track)
            .ToListAsync(ct);
    }

    #endregion

    #region Track Queries

    public Task<Track?> GetTrackAsync(Guid id, CancellationToken ct = default)
    {
        return mediaContext.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .ThenInclude(album => album.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Artist)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync(ct);
    }

    public IQueryable<TrackUser> GetTracks(Guid userId)
    {
        return mediaContext.TrackUser
            .AsNoTracking()
            .Where(tu => tu.UserId == userId)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist);
    }

    public async Task LikeTrackAsync(Guid userId, Track track, bool liked, CancellationToken ct = default)
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
                .FirstOrDefaultAsync(tu => tu.TrackId == track.Id && tu.UserId == userId, ct);

            if (trackUser is not null)
            {
                mediaContext.TrackUser.Remove(trackUser);
                await mediaContext.SaveChangesAsync(ct);
            }
        }
    }

    public async Task RecordPlaybackAsync(Guid trackId, Guid userId, CancellationToken ct = default)
    {
        await mediaContext.MusicPlays.AddAsync(new(userId, trackId), ct);
        await mediaContext.SaveChangesAsync(ct);
    }

    public Task<Track?> GetTrackWithIncludesAsync(Guid id, CancellationToken ct = default)
    {
        return mediaContext.Tracks
            .AsNoTracking()
            .Where(track => track.Id == id)
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Lyric[]?> UpdateTrackLyricsAsync(Track track, string lyricsJson, CancellationToken ct = default)
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

    public Task<List<CarouselResponseItemDto>> GetCarouselPlaylistsAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Include(playlist => playlist.Tracks)
            .ThenInclude(trackUser => trackUser.Track)
            .Select(playlist => new CarouselResponseItemDto(playlist))
            .Take(36)
            .ToListAsync(ct);
    }

    public Task<Playlist?> GetPlaylistAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        return mediaContext.Playlists
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
            .FirstOrDefaultAsync(ct);
    }

    #endregion

    #region Home Page Methods

    public IQueryable<Album> GetLatestAlbums()
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .OrderByDescending(album => album.CreatedAt);
    }

    public IQueryable<Artist> GetLatestArtists()
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .Include(artist => artist.Images.Where(image => image.Type == "thumb"))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .OrderByDescending(artist => artist.CreatedAt);
    }

    public IQueryable<MusicGenre> GetLatestGenres()
    {
        return mediaContext.MusicGenres
            .AsNoTracking()
            .Where(genre => genre.MusicGenreTracks.Any())
            .Include(genre => genre.MusicGenreTracks)
            .OrderByDescending(genre => genre.MusicGenreTracks.Count);
    }

    public Task<List<ArtistTrack>> GetFavoriteArtistAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.UserId == userId)
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .ThenInclude(artist => artist.Images.Where(image => image.Type == "thumb"))
            .SelectMany(p => p.Track.ArtistTrack)
            .ToListAsync(ct);
    }

    public Task<List<AlbumTrack>> GetFavoriteAlbumAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.UserId == userId)
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .SelectMany(p => p.Track.AlbumTrack)
            .ToListAsync(ct);
    }

    public Task<List<PlaylistTrack>> GetFavoritePlaylistAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(musicPlay => musicPlay.Track.PlaylistTrack.All(pt => pt.Playlist.UserId == userId))
            .Include(musicPlay => musicPlay.Track)
            .ThenInclude(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .SelectMany(p => p.Track.PlaylistTrack)
            .ToListAsync(ct);
    }

    public IQueryable<ArtistUser> GetFavoriteArtists(Guid userId)
    {
        return mediaContext.ArtistUser
            .AsNoTracking()
            .Where(artistUser => artistUser.UserId == userId)
            .Include(artistUser => artistUser.Artist)
            .ThenInclude(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artistUser => artistUser.Artist)
            .ThenInclude(artist => artist.Images.Where(image => image.Type == "thumb"));
    }

    public IQueryable<AlbumUser> GetFavoriteAlbums(Guid userId)
    {
        return mediaContext.AlbumUser
            .AsNoTracking()
            .Where(albumUser => albumUser.UserId == userId)
            .Include(albumUser => albumUser.Album)
            .ThenInclude(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track);
    }

    #endregion

    #region Collection Operations (for CollectionsController)

    public IQueryable<TrackUser> GetFavoriteTracks(Guid userId)
    {
        return mediaContext.TrackUser
            .AsNoTracking()
            .Where(trackUser => trackUser.UserId == userId)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(trackUser => trackUser.Track)
            .ThenInclude(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album);
    }

    public Task<List<ArtistTrack>> GetArtistTracksForCollectionAsync(List<Guid> artistIds, CancellationToken ct = default)
    {
        return mediaContext.ArtistTrack
            .AsNoTracking()
            .Where(artistTrack => artistIds.Contains(artistTrack.ArtistId))
            .Include(artistTrack => artistTrack.Track)
            .ToListAsync(ct);
    }

    #endregion

    #region Search Operations

    public Task<List<Guid>> SearchArtistIdsAsync(string normalizedQuery, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => MediaContext.NormalizeSearch(artist.Name).Contains(normalizedQuery))
            .Select(artist => artist.Id)
            .ToListAsync(ct);
    }

    public Task<List<Guid>> SearchAlbumIdsAsync(string normalizedQuery, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => MediaContext.NormalizeSearch(album.Name).Contains(normalizedQuery))
            .Select(album => album.Id)
            .ToListAsync(ct);
    }

    public Task<List<Guid>> SearchPlaylistIdsAsync(string normalizedQuery, CancellationToken ct = default)
    {
        return mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => MediaContext.NormalizeSearch(playlist.Name).Contains(normalizedQuery))
            .Select(playlist => playlist.Id)
            .ToListAsync(ct);
    }

    public Task<List<Guid>> SearchTrackIdsAsync(string normalizedQuery, CancellationToken ct = default)
    {
        return mediaContext.Tracks
            .AsNoTracking()
            .Where(track => MediaContext.NormalizeSearch(track.Name).Contains(normalizedQuery))
            .Select(track => track.Id)
            .ToListAsync(ct);
    }

    public Task<List<Artist>> GetArtistsByIdsAsync(List<Guid> artistIds, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => artistIds.Contains(artist.Id))
            .Include(artist => artist.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Track)
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .ToListAsync(ct);
    }

    public Task<List<Album>> GetAlbumsByIdsAsync(List<Guid> albumIds, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => albumIds.Contains(album.Id))
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(album => album.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.TrackUser)
            .ToListAsync(ct);
    }

    public Task<List<Playlist>> GetPlaylistsByIdsAsync(List<Guid> playlistIds, CancellationToken ct = default)
    {
        return mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Include(playlist => playlist.Tracks)
            .ThenInclude(playlistTrack => playlistTrack.Track)
            .ThenInclude(track => track.TrackUser)
            .ToListAsync(ct);
    }

    public Task<List<Track>> GetTracksByIdsAsync(List<Guid> trackIds, CancellationToken ct = default)
    {
        return mediaContext.Tracks
            .AsNoTracking()
            .Where(track => trackIds.Contains(track.Id))
            .Include(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .Include(track => track.AlbumTrack)
            .ThenInclude(albumTrack => albumTrack.Album)
            .Include(track => track.PlaylistTrack)
            .ThenInclude(playlistTrack => playlistTrack.Playlist)
            .Include(track => track.TrackUser)
            .ToListAsync(ct);
    }

    #endregion

    #region Playlist Management

    public Task<PlaylistTrack?> GetPlaylistTrackAsync(Guid userId, Guid playlistId, Guid trackId, CancellationToken ct = default)
    {
        return mediaContext.PlaylistTrack
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
            .FirstOrDefaultAsync(ct);
    }

    public Task<AlbumTrack?> GetAlbumTrackAsync(Guid userId, Guid albumId, Guid trackId, CancellationToken ct = default)
    {
        return mediaContext.AlbumTrack
            .Where(at => at.AlbumId == albumId && at.TrackId == trackId)
            .Include(at => at.Track)
            .Include(at => at.Album)
            .ThenInclude(album => album.AlbumTrack
                .OrderBy(albumTrack => albumTrack.Track.DiscNumber)
                .ThenBy(albumTrack => albumTrack.Track.TrackNumber))
            .ThenInclude(albumTrack => albumTrack.Track)
            .ThenInclude(track => track.ArtistTrack)
            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync(ct);
    }

    public Task<ArtistTrack?> GetArtistTrackAsync(Guid userId, Guid artistId, Guid trackId, CancellationToken ct = default)
    {
        return mediaContext.ArtistTrack
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
            .FirstOrDefaultAsync(ct);
    }

    public Task<MusicGenreTrack?> GetGenreTrackAsync(Guid userId, Guid genreId, Guid trackId, CancellationToken ct = default)
    {
        return mediaContext.MusicGenreTrack
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

            .FirstOrDefaultAsync(ct);
    }

    #endregion

}