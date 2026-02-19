using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Data.Repositories;

public class MusicRepository(MediaContext mediaContext, IDbContextFactory<MediaContext> contextFactory)
{
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    #region Artist Queries

    public Task<Artist?> GetArtistAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .AsSplitQuery()
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
            .AsSplitQuery()
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

    #region Projection Methods — Artist Cards

    public Task<List<ArtistCardDto>> GetArtistCardsAsync(Guid userId, string letter, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .ForUser(userId)
            .Where(artist => (letter == "_" || letter == "#")
                ? Letters.Any(p => artist.Name.StartsWith(p))
                : artist.Name.StartsWith(letter))
            .Where(artist => artist.ArtistTrack.Any())
            .OrderBy(artist => artist.Name)
            .Select(artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public Task<List<ArtistCardDto>> GetLatestArtistCardsAsync(int take = 36, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .OrderByDescending(artist => artist.CreatedAt)
            .Select(artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<ArtistCardDto>> GetFavoriteArtistCardsAsync(Guid userId, int take = 36, CancellationToken ct = default)
    {
        return mediaContext.ArtistUser
            .AsNoTracking()
            .Where(artistUser => artistUser.UserId == userId)
            .Select(artistUser => new ArtistCardDto
            {
                Id = artistUser.Artist.Id,
                Name = artistUser.Artist.Name,
                Cover = artistUser.Artist.Cover,
                Disambiguation = artistUser.Artist.Disambiguation,
                Description = artistUser.Artist.Description,
                ColorPalette = artistUser.Artist._colorPalette,
                LibraryId = artistUser.Artist.LibraryId,
                Folder = artistUser.Artist.Folder,
                TrackCount = artistUser.Artist.ArtistTrack.Count(),
                ThumbImagePath = artistUser.Artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<ArtistCardDto>> GetArtistCardsByIdsAsync(List<Guid> artistIds, CancellationToken ct = default)
    {
        return mediaContext.Artists
            .AsNoTracking()
            .Where(artist => artistIds.Contains(artist.Id))
            .Select(artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    #endregion

    #region Projection Methods — Album Cards

    public Task<List<AlbumCardDto>> GetAlbumCardsAsync(Guid userId, string letter, string language, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .ForUser(userId)
            .Where(album => (letter == "_" || letter == "#")
                ? Letters.Any(p => album.Name.StartsWith(p))
                : album.Name.StartsWith(letter))
            .Where(album => album.AlbumTrack.Any(at => at.Track.Duration != null))
            .OrderBy(album => album.Name)
            .Select(album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(at => at.Track.Duration != null),
                TranslatedDescription = album.Translations
                    .Where(t => t.Iso31661 == language)
                    .Select(t => t.Description)
                    .FirstOrDefault(),
                BackgroundImagePath = album.Images
                    .Where(image => image.Type == "background")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
                BackgroundImageColorPalette = album.Images
                    .Where(image => image.Type == "background")
                    .Select(image => image._colorPalette)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public Task<List<AlbumCardDto>> GetLatestAlbumCardsAsync(int take = 36, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .OrderByDescending(album => album.CreatedAt)
            .Select(album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<AlbumCardDto>> GetFavoriteAlbumCardsAsync(Guid userId, int take = 36, CancellationToken ct = default)
    {
        return mediaContext.AlbumUser
            .AsNoTracking()
            .Where(albumUser => albumUser.UserId == userId)
            .Select(albumUser => new AlbumCardDto
            {
                Id = albumUser.Album.Id,
                Name = albumUser.Album.Name,
                Cover = albumUser.Album.Cover,
                Disambiguation = albumUser.Album.Disambiguation,
                Description = albumUser.Album.Description,
                ColorPalette = albumUser.Album._colorPalette,
                LibraryId = albumUser.Album.LibraryId,
                Folder = albumUser.Album.Folder,
                Year = albumUser.Album.Year,
                TrackCount = albumUser.Album.AlbumTrack.Count()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<AlbumCardDto>> GetAlbumCardsByIdsAsync(List<Guid> albumIds, CancellationToken ct = default)
    {
        return mediaContext.Albums
            .AsNoTracking()
            .Where(album => albumIds.Contains(album.Id))
            .Select(album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count()
            })
            .ToListAsync(ct);
    }

    #endregion

    #region Projection Methods — Playlist Cards

    public Task<List<PlaylistCardDto>> GetPlaylistCardsAsync(Guid userId, int take = 36, CancellationToken ct = default)
    {
        return mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Select(playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette,
                TrackCount = playlist.Tracks.Count()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<PlaylistCardDto>> GetPlaylistCardsByIdsAsync(List<Guid> playlistIds, CancellationToken ct = default)
    {
        return mediaContext.Playlists
            .AsNoTracking()
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Select(playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette,
                TrackCount = playlist.Tracks.Count()
            })
            .ToListAsync(ct);
    }

    #endregion

    #region Projection Methods — Genre Cards

    public Task<List<MusicGenreCardDto>> GetLatestGenreCardsAsync(int take = 36, CancellationToken ct = default)
    {
        return mediaContext.MusicGenres
            .AsNoTracking()
            .Where(genre => genre.MusicGenreTracks.Any())
            .OrderByDescending(genre => genre.MusicGenreTracks.Count())
            .Select(genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count()
            })
            .Take(take)
            .ToListAsync(ct);
    }

    #endregion

    #region Projection Methods — Top Music (Favorites)

    public Task<TopMusicItemDto?> GetTopArtistAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .SelectMany(mp => mp.Track.ArtistTrack)
            .GroupBy(at => new { at.Artist.Id, at.Artist.Name, at.Artist.Cover, ColorPalette = at.Artist._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "artist"
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<TopMusicItemDto?> GetTopAlbumAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .SelectMany(mp => mp.Track.AlbumTrack)
            .GroupBy(at => new { at.Album.Id, at.Album.Name, at.Album.Cover, ColorPalette = at.Album._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "album"
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<TopMusicItemDto?> GetTopPlaylistAsync(Guid userId, CancellationToken ct = default)
    {
        return mediaContext.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.Track.PlaylistTrack.Any(pt => pt.Playlist.UserId == userId))
            .SelectMany(mp => mp.Track.PlaylistTrack)
            .Where(pt => pt.Playlist.UserId == userId)
            .GroupBy(pt => new { pt.Playlist.Id, pt.Playlist.Name, pt.Playlist.Cover, ColorPalette = pt.Playlist._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "playlist"
            })
            .FirstOrDefaultAsync(ct);
    }

    #endregion

    #region Projection Methods — Search Cross-Reference

    public Task<List<Guid>> GetArtistIdsFromAlbumsAsync(List<Guid> albumIds, CancellationToken ct = default)
    {
        return mediaContext.AlbumTrack
            .AsNoTracking()
            .Where(at => albumIds.Contains(at.AlbumId))
            .SelectMany(at => at.Track.ArtistTrack)
            .Select(at => at.ArtistId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<List<Guid>> GetArtistIdsFromPlaylistTracksAsync(List<Guid> playlistIds, CancellationToken ct = default)
    {
        return mediaContext.PlaylistTrack
            .AsNoTracking()
            .Where(pt => playlistIds.Contains(pt.PlaylistId))
            .SelectMany(pt => pt.Track.ArtistTrack)
            .Select(at => at.ArtistId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<List<Guid>> GetArtistIdsFromTracksAsync(List<Guid> trackIds, CancellationToken ct = default)
    {
        return mediaContext.ArtistTrack
            .AsNoTracking()
            .Where(at => trackIds.Contains(at.TrackId))
            .Select(at => at.ArtistId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<List<Guid>> GetAlbumIdsFromTracksAsync(List<Guid> trackIds, CancellationToken ct = default)
    {
        return mediaContext.AlbumTrack
            .AsNoTracking()
            .Where(at => trackIds.Contains(at.TrackId))
            .Select(at => at.AlbumId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<List<SearchTrackCardDto>> SearchTrackCardsAsync(List<Guid> trackIds, string country, CancellationToken ct = default)
    {
        return mediaContext.Tracks
            .AsNoTracking()
            .Where(track => trackIds.Contains(track.Id))
            .Select(track => new SearchTrackCardDto
            {
                Id = track.Id,
                Name = track.Name,
                FolderId = track.FolderId,
                Folder = track.Folder,
                Filename = track.Filename,
                Cover = track.Cover,
                ColorPalette = track._colorPalette,
                Duration = track.Duration,
                DiscNumber = track.DiscNumber,
                TrackNumber = track.TrackNumber,
                Quality = track.Quality,
                UpdatedAt = track.UpdatedAt,
                Favorite = track.TrackUser.Any(),
                AlbumId = track.AlbumTrack.Select(at => at.AlbumId.ToString()).FirstOrDefault(),
                AlbumName = track.AlbumTrack.Select(at => at.Album.Name).FirstOrDefault(),
                AlbumCover = track.AlbumTrack.Select(at => at.Album.Cover).FirstOrDefault(),
                AlbumColorPalette = track.AlbumTrack.Select(at => at.Album._colorPalette).FirstOrDefault(),
                ArtistCover = track.ArtistTrack.Select(at => at.Artist.Cover).FirstOrDefault(),
                ArtistColorPalette = track.ArtistTrack.Select(at => at.Artist._colorPalette).FirstOrDefault(),
                Artists = track.ArtistTrack.Select(at => new SearchTrackArtistDto
                {
                    Id = at.ArtistId,
                    Name = at.Artist.Name
                }).ToList(),
                Albums = track.AlbumTrack.Select(at => new SearchTrackAlbumDto
                {
                    Id = at.AlbumId,
                    Name = at.Album.Name
                }).ToList()
            })
            .ToListAsync(ct);
    }

    #endregion

    #region Parallel Music Start Page

    public async Task<MusicStartPageData> GetMusicStartPageAsync(Guid userId, CancellationToken ct = default)
    {
        // Run 3 groups in parallel — each group gets its own DbContext
        Task<(TopMusicItemDto?, TopMusicItemDto?, TopMusicItemDto?)> topTask = Task.Run(async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
            TopMusicItemDto? artist = await GetTopArtistQuery(ctx, userId).FirstOrDefaultAsync(ct);
            TopMusicItemDto? album = await GetTopAlbumQuery(ctx, userId).FirstOrDefaultAsync(ct);
            TopMusicItemDto? playlist = await GetTopPlaylistQuery(ctx, userId).FirstOrDefaultAsync(ct);
            return (artist, album, playlist);
        }, ct);

        Task<(List<ArtistCardDto>, List<AlbumCardDto>, List<PlaylistCardDto>)> favoritesTask = Task.Run(async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
            List<ArtistCardDto> artists = await GetFavoriteArtistCardsQuery(ctx, userId).Take(36).ToListAsync(ct);
            List<AlbumCardDto> albums = await GetFavoriteAlbumCardsQuery(ctx, userId).Take(36).ToListAsync(ct);
            List<PlaylistCardDto> playlists = await GetPlaylistCardsQuery(ctx, userId).Take(36).ToListAsync(ct);
            return (artists, albums, playlists);
        }, ct);

        Task<(List<ArtistCardDto>, List<MusicGenreCardDto>, List<AlbumCardDto>)> latestTask = Task.Run(async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(ct);
            List<ArtistCardDto> artists = await GetLatestArtistCardsQuery(ctx).Take(36).ToListAsync(ct);
            List<MusicGenreCardDto> genres = await GetLatestGenreCardsQuery(ctx).Take(36).ToListAsync(ct);
            List<AlbumCardDto> albums = await GetLatestAlbumCardsQuery(ctx).Take(36).ToListAsync(ct);
            return (artists, genres, albums);
        }, ct);

        await Task.WhenAll(topTask, favoritesTask, latestTask);

        (TopMusicItemDto? topArtist, TopMusicItemDto? topAlbum, TopMusicItemDto? topPlaylist) = topTask.Result;
        (List<ArtistCardDto> favArtists, List<AlbumCardDto> favAlbums, List<PlaylistCardDto> playlists) = favoritesTask.Result;
        (List<ArtistCardDto> latestArtists, List<MusicGenreCardDto> latestGenres, List<AlbumCardDto> latestAlbums) = latestTask.Result;

        return new MusicStartPageData
        {
            TopArtist = topArtist,
            TopAlbum = topAlbum,
            TopPlaylist = topPlaylist,
            FavoriteArtists = favArtists,
            FavoriteAlbums = favAlbums,
            Playlists = playlists,
            LatestArtists = latestArtists,
            LatestGenres = latestGenres,
            LatestAlbums = latestAlbums
        };
    }

    // Static query builders for parallel execution with arbitrary MediaContext instances

    private static IQueryable<TopMusicItemDto> GetTopArtistQuery(MediaContext ctx, Guid userId)
    {
        return ctx.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .SelectMany(mp => mp.Track.ArtistTrack)
            .GroupBy(at => new { at.Artist.Id, at.Artist.Name, at.Artist.Cover, ColorPalette = at.Artist._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "artist"
            });
    }

    private static IQueryable<TopMusicItemDto> GetTopAlbumQuery(MediaContext ctx, Guid userId)
    {
        return ctx.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .SelectMany(mp => mp.Track.AlbumTrack)
            .GroupBy(at => new { at.Album.Id, at.Album.Name, at.Album.Cover, ColorPalette = at.Album._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "album"
            });
    }

    private static IQueryable<TopMusicItemDto> GetTopPlaylistQuery(MediaContext ctx, Guid userId)
    {
        return ctx.MusicPlays
            .AsNoTracking()
            .Where(mp => mp.Track.PlaylistTrack.Any(pt => pt.Playlist.UserId == userId))
            .SelectMany(mp => mp.Track.PlaylistTrack)
            .Where(pt => pt.Playlist.UserId == userId)
            .GroupBy(pt => new { pt.Playlist.Id, pt.Playlist.Name, pt.Playlist.Cover, ColorPalette = pt.Playlist._colorPalette })
            .OrderByDescending(g => g.Count())
            .Select(g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "playlist"
            });
    }

    private static IQueryable<ArtistCardDto> GetFavoriteArtistCardsQuery(MediaContext ctx, Guid userId)
    {
        return ctx.ArtistUser
            .AsNoTracking()
            .Where(artistUser => artistUser.UserId == userId)
            .Select(artistUser => new ArtistCardDto
            {
                Id = artistUser.Artist.Id,
                Name = artistUser.Artist.Name,
                Cover = artistUser.Artist.Cover,
                Disambiguation = artistUser.Artist.Disambiguation,
                Description = artistUser.Artist.Description,
                ColorPalette = artistUser.Artist._colorPalette,
                LibraryId = artistUser.Artist.LibraryId,
                Folder = artistUser.Artist.Folder,
                TrackCount = artistUser.Artist.ArtistTrack.Count(),
                ThumbImagePath = artistUser.Artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            });
    }

    private static IQueryable<AlbumCardDto> GetFavoriteAlbumCardsQuery(MediaContext ctx, Guid userId)
    {
        return ctx.AlbumUser
            .AsNoTracking()
            .Where(albumUser => albumUser.UserId == userId)
            .Select(albumUser => new AlbumCardDto
            {
                Id = albumUser.Album.Id,
                Name = albumUser.Album.Name,
                Cover = albumUser.Album.Cover,
                Disambiguation = albumUser.Album.Disambiguation,
                Description = albumUser.Album.Description,
                ColorPalette = albumUser.Album._colorPalette,
                LibraryId = albumUser.Album.LibraryId,
                Folder = albumUser.Album.Folder,
                Year = albumUser.Album.Year,
                TrackCount = albumUser.Album.AlbumTrack.Count()
            });
    }

    private static IQueryable<PlaylistCardDto> GetPlaylistCardsQuery(MediaContext ctx, Guid userId)
    {
        return ctx.Playlists
            .AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Select(playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette,
                TrackCount = playlist.Tracks.Count()
            });
    }

    private static IQueryable<ArtistCardDto> GetLatestArtistCardsQuery(MediaContext ctx)
    {
        return ctx.Artists
            .AsNoTracking()
            .Where(artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .OrderByDescending(artist => artist.CreatedAt)
            .Select(artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist.Images
                    .Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault()
            });
    }

    private static IQueryable<MusicGenreCardDto> GetLatestGenreCardsQuery(MediaContext ctx)
    {
        return ctx.MusicGenres
            .AsNoTracking()
            .Where(genre => genre.MusicGenreTracks.Any())
            .OrderByDescending(genre => genre.MusicGenreTracks.Count())
            .Select(genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count()
            });
    }

    private static IQueryable<AlbumCardDto> GetLatestAlbumCardsQuery(MediaContext ctx)
    {
        return ctx.Albums
            .AsNoTracking()
            .Where(album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .OrderByDescending(album => album.CreatedAt)
            .Select(album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count()
            });
    }

    #endregion

}

public class MusicStartPageData
{
    public TopMusicItemDto? TopArtist { get; set; }
    public TopMusicItemDto? TopAlbum { get; set; }
    public TopMusicItemDto? TopPlaylist { get; set; }
    public List<ArtistCardDto> FavoriteArtists { get; set; } = [];
    public List<AlbumCardDto> FavoriteAlbums { get; set; } = [];
    public List<PlaylistCardDto> Playlists { get; set; } = [];
    public List<ArtistCardDto> LatestArtists { get; set; } = [];
    public List<MusicGenreCardDto> LatestGenres { get; set; } = [];
    public List<AlbumCardDto> LatestAlbums { get; set; } = [];
}

public class ArtistCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Disambiguation { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public Ulid? LibraryId { get; set; }
    public string? Folder { get; set; }
    public int TrackCount { get; set; }
    public string? ThumbImagePath { get; set; }
}

public class AlbumCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Disambiguation { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public Ulid LibraryId { get; set; }
    public string? Folder { get; set; }
    public int Year { get; set; }
    public int TrackCount { get; set; }
    public string? TranslatedDescription { get; set; }
    public string? BackgroundImagePath { get; set; }
    public string? BackgroundImageColorPalette { get; set; }
}

public class PlaylistCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string? Description { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public int TrackCount { get; set; }
}

public class MusicGenreCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int TrackCount { get; set; }
}

public class TopMusicItemDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Cover { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string Type { get; set; } = null!;
}

public class SearchTrackCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Ulid? FolderId { get; set; }
    public string? Folder { get; set; }
    public string? Filename { get; set; }
    public string? Cover { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string? Duration { get; set; }
    public int DiscNumber { get; set; }
    public int TrackNumber { get; set; }
    public int? Quality { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Favorite { get; set; }
    public string? AlbumId { get; set; }
    public string? AlbumName { get; set; }
    public string? AlbumCover { get; set; }
    public string? AlbumColorPalette { get; set; }
    public string? ArtistCover { get; set; }
    public string? ArtistColorPalette { get; set; }
    public List<SearchTrackArtistDto> Artists { get; set; } = [];
    public List<SearchTrackAlbumDto> Albums { get; set; } = [];
}

public class SearchTrackArtistDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public class SearchTrackAlbumDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}