// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public partial class MusicRepository
{
    #region Home Page Methods

    public async Task<List<Album>> GetLatestAlbums(CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .Include(navigationPropertyPath: album => album.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Track)
            .OrderByDescending(keySelector: album => album.CreatedAt)
            .ThenBy(keySelector: album => album.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Artist>> GetLatestArtists(CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Where(predicate: artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .Include(navigationPropertyPath: artist => artist.Images.Where(image => image.Type == "thumb"))
            .Include(navigationPropertyPath: artist => artist.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Track)
            .OrderByDescending(keySelector: artist => artist.CreatedAt)
            .ThenBy(keySelector: artist => artist.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<MusicGenre>> GetLatestGenres(CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .Include(navigationPropertyPath: genre => genre.MusicGenreTracks)
            .OrderByDescending(keySelector: genre => genre.MusicGenreTracks.Count)
            .ThenBy(keySelector: genre => genre.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<ArtistTrack>> GetFavoriteArtistAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: musicPlay => musicPlay.UserId == userId)
            .Include(navigationPropertyPath: musicPlay => musicPlay.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
                        .ThenInclude(navigationPropertyPath: artist => artist.Images.Where(image => image.Type == "thumb"))
            .SelectMany(selector: p => p.Track.ArtistTrack)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<AlbumTrack>> GetFavoriteAlbumAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: musicPlay => musicPlay.UserId == userId)
            .Include(navigationPropertyPath: musicPlay => musicPlay.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .SelectMany(selector: p => p.Track.AlbumTrack)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<PlaylistTrack>> GetFavoritePlaylistAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: musicPlay =>
                musicPlay.Track.PlaylistTrack.All(pt => pt.Playlist.UserId == userId)
            )
            .Include(navigationPropertyPath: musicPlay => musicPlay.Track)
                .ThenInclude(navigationPropertyPath: track => track.PlaylistTrack)
                    .ThenInclude(navigationPropertyPath: playlistTrack => playlistTrack.Playlist)
            .SelectMany(selector: p => p.Track.PlaylistTrack)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<ArtistUser>> GetFavoriteArtists(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .ArtistUser.AsNoTracking()
            .Where(predicate: artistUser => artistUser.UserId == userId)
            .Include(navigationPropertyPath: artistUser => artistUser.Artist)
                .ThenInclude(navigationPropertyPath: artist => artist.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Track)
            .Include(navigationPropertyPath: artistUser => artistUser.Artist)
                .ThenInclude(navigationPropertyPath: artist => artist.Images.Where(image => image.Type == "thumb"))
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<AlbumUser>> GetFavoriteAlbums(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .AlbumUser.AsNoTracking()
            .Where(predicate: albumUser => albumUser.UserId == userId)
            .Include(navigationPropertyPath: albumUser => albumUser.Album)
                .ThenInclude(navigationPropertyPath: album => album.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Track)
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Collection Operations (for CollectionsController)

    public async Task<List<TrackUser>> GetFavoriteTracks(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .TrackUser.AsNoTracking()
            .Where(predicate: trackUser => trackUser.UserId == userId)
            .Include(navigationPropertyPath: trackUser => trackUser.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: trackUser => trackUser.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<ArtistTrack>> GetArtistTracksForCollectionAsync(
        List<Guid> artistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .ArtistTrack.AsNoTracking()
            .Where(predicate: artistTrack => artistIds.Contains(artistTrack.ArtistId))
            .Include(navigationPropertyPath: artistTrack => artistTrack.Track)
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Projection Methods — Genre Cards

    public async Task<List<MusicGenreCardDto>> GetLatestGenreCardsAsync(
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .OrderByDescending(keySelector: genre => genre.MusicGenreTracks.Count())
            .ThenBy(keySelector: genre => genre.Id)
            .Select(selector: genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Projection Methods — Top Music (Favorites)

    public async Task<TopMusicItemDto?> GetTopArtistAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.UserId == userId)
            .SelectMany(selector: mp => mp.Track.ArtistTrack)
            .GroupBy(keySelector: at => new
            {
                at.Artist.Id,
                at.Artist.Name,
                at.Artist.Cover,
                ColorPalette = at.Artist._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "artist",
            })
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<TopMusicItemDto?> GetTopAlbumAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.UserId == userId)
            .SelectMany(selector: mp => mp.Track.AlbumTrack)
            .GroupBy(keySelector: at => new
            {
                at.Album.Id,
                at.Album.Name,
                at.Album.Cover,
                ColorPalette = at.Album._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "album",
            })
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<TopMusicItemDto?> GetTopPlaylistAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.Track.PlaylistTrack.Any(pt => pt.Playlist.UserId == userId))
            .SelectMany(selector: mp => mp.Track.PlaylistTrack)
            .Where(predicate: pt => pt.Playlist.UserId == userId)
            .GroupBy(keySelector: pt => new
            {
                pt.Playlist.Id,
                pt.Playlist.Name,
                pt.Playlist.Cover,
                ColorPalette = pt.Playlist._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "playlist",
            })
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    #endregion

    #region Parallel Music Start Page

    public async Task<MusicStartPageData> GetMusicStartPageAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        // Run 3 groups in parallel — each group gets its own DbContext
        Task<(TopMusicItemDto?, TopMusicItemDto?, TopMusicItemDto?)> topTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                TopMusicItemDto? artist = await GetTopArtistQuery(ctx: ctx, userId: userId)
                    .FirstOrDefaultAsync(cancellationToken: ct);
                TopMusicItemDto? album = await GetTopAlbumQuery(ctx: ctx, userId: userId)
                    .FirstOrDefaultAsync(cancellationToken: ct);
                TopMusicItemDto? playlist = await GetTopPlaylistQuery(ctx: ctx, userId: userId)
                    .FirstOrDefaultAsync(cancellationToken: ct);
                return (artist, album, playlist);
            },
            cancellationToken: ct
        );

        Task<(List<ArtistCardDto>, List<AlbumCardDto>, List<PlaylistCardDto>)> favoritesTask =
            Task.Run(
                function: async () =>
                {
                    await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                    List<ArtistCardDto> artists = await GetFavoriteArtistCardsQuery(ctx: ctx, userId: userId)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    List<AlbumCardDto> albums = await GetFavoriteAlbumCardsQuery(ctx: ctx, userId: userId)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    List<PlaylistCardDto> playlists = await GetPlaylistCardsQuery(ctx: ctx, userId: userId)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    return (artists, albums, playlists);
                },
                cancellationToken: ct
            );

        Task<(List<ArtistCardDto>, List<MusicGenreCardDto>, List<AlbumCardDto>)> latestTask =
            Task.Run(
                function: async () =>
                {
                    await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                    List<ArtistCardDto> artists = await GetLatestArtistCardsQuery(ctx: ctx)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    List<MusicGenreCardDto> genres = await GetLatestGenreCardsQuery(ctx: ctx)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    List<AlbumCardDto> albums = await GetLatestAlbumCardsQuery(ctx: ctx)
                        .Take(count: 36)
                        .ToListAsync(cancellationToken: ct);
                    return (artists, genres, albums);
                },
                cancellationToken: ct
            );

        await Task.WhenAll(tasks: [topTask, favoritesTask, latestTask]);

        (TopMusicItemDto? topArtist, TopMusicItemDto? topAlbum, TopMusicItemDto? topPlaylist) =
            topTask.Result;
        (
            List<ArtistCardDto> favArtists,
            List<AlbumCardDto> favAlbums,
            List<PlaylistCardDto> playlists
        ) = favoritesTask.Result;
        (
            List<ArtistCardDto> latestArtists,
            List<MusicGenreCardDto> latestGenres,
            List<AlbumCardDto> latestAlbums
        ) = latestTask.Result;

        return new()
        {
            TopArtist = topArtist,
            TopAlbum = topAlbum,
            TopPlaylist = topPlaylist,
            FavoriteArtists = favArtists,
            FavoriteAlbums = favAlbums,
            Playlists = playlists,
            LatestArtists = latestArtists,
            LatestGenres = latestGenres,
            LatestAlbums = latestAlbums,
        };
    }

    // Static query builders for parallel execution with arbitrary MediaContext instances

    private static IQueryable<TopMusicItemDto> GetTopArtistQuery(MediaContext ctx, Guid userId)
    {
        return ctx
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.UserId == userId)
            .SelectMany(selector: mp => mp.Track.ArtistTrack)
            .GroupBy(keySelector: at => new
            {
                at.Artist.Id,
                at.Artist.Name,
                at.Artist.Cover,
                ColorPalette = at.Artist._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "artist",
            });
    }

    private static IQueryable<TopMusicItemDto> GetTopAlbumQuery(MediaContext ctx, Guid userId)
    {
        return ctx
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.UserId == userId)
            .SelectMany(selector: mp => mp.Track.AlbumTrack)
            .GroupBy(keySelector: at => new
            {
                at.Album.Id,
                at.Album.Name,
                at.Album.Cover,
                ColorPalette = at.Album._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "album",
            });
    }

    private static IQueryable<TopMusicItemDto> GetTopPlaylistQuery(MediaContext ctx, Guid userId)
    {
        return ctx
            .MusicPlays.AsNoTracking()
            .Where(predicate: mp => mp.Track.PlaylistTrack.Any(pt => pt.Playlist.UserId == userId))
            .SelectMany(selector: mp => mp.Track.PlaylistTrack)
            .Where(predicate: pt => pt.Playlist.UserId == userId)
            .GroupBy(keySelector: pt => new
            {
                pt.Playlist.Id,
                pt.Playlist.Name,
                pt.Playlist.Cover,
                ColorPalette = pt.Playlist._colorPalette ?? string.Empty,
            })
            .OrderByDescending(keySelector: g => g.Count())
            .ThenBy(keySelector: g => g.Key.Id)
            .Select(selector: g => new TopMusicItemDto
            {
                Id = g.Key.Id.ToString(),
                Name = g.Key.Name,
                Cover = g.Key.Cover,
                ColorPalette = g.Key.ColorPalette,
                Type = "playlist",
            });
    }

    private static IQueryable<ArtistCardDto> GetFavoriteArtistCardsQuery(
        MediaContext ctx,
        Guid userId
    )
    {
        return ctx
            .ArtistUser.AsNoTracking()
            .Where(predicate: artistUser => artistUser.UserId == userId)
            .Select(selector: artistUser => new ArtistCardDto
            {
                Id = artistUser.Artist.Id,
                Name = artistUser.Artist.Name,
                Cover = artistUser.Artist.Cover,
                Disambiguation = artistUser.Artist.Disambiguation,
                Description = artistUser.Artist.Description,
                ColorPalette = artistUser.Artist._colorPalette ?? string.Empty,
                LibraryId = artistUser.Artist.LibraryId,
                Folder = artistUser.Artist.Folder,
                TrackCount = artistUser.Artist.ArtistTrack.Count(),
                ThumbImagePath = artistUser
                    .Artist.Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            });
    }

    private static IQueryable<AlbumCardDto> GetFavoriteAlbumCardsQuery(
        MediaContext ctx,
        Guid userId
    )
    {
        return ctx
            .AlbumUser.AsNoTracking()
            .Where(predicate: albumUser => albumUser.UserId == userId)
            .Select(selector: albumUser => new AlbumCardDto
            {
                Id = albumUser.Album.Id,
                Name = albumUser.Album.Name,
                Cover = albumUser.Album.Cover,
                Disambiguation = albumUser.Album.Disambiguation,
                Description = albumUser.Album.Description,
                ColorPalette = albumUser.Album._colorPalette ?? string.Empty,
                LibraryId = albumUser.Album.LibraryId,
                Folder = albumUser.Album.Folder,
                Year = albumUser.Album.Year,
                TrackCount = albumUser.Album.AlbumTrack.Count(),
            });
    }

    private static IQueryable<PlaylistCardDto> GetPlaylistCardsQuery(MediaContext ctx, Guid userId)
    {
        return ctx
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .Select(selector: playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette ?? string.Empty,
                TrackCount = playlist.Tracks.Count(),
            });
    }

    private static IQueryable<ArtistCardDto> GetLatestArtistCardsQuery(MediaContext ctx)
    {
        return ctx
            .Artists.AsNoTracking()
            .Where(predicate: artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .OrderByDescending(keySelector: artist => artist.CreatedAt)
            .ThenBy(keySelector: artist => artist.Id)
            .Select(selector: artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette ?? string.Empty,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist
                    .Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            });
    }

    private static IQueryable<MusicGenreCardDto> GetLatestGenreCardsQuery(MediaContext ctx)
    {
        return ctx
            .MusicGenres.AsNoTracking()
            .Where(predicate: genre => genre.MusicGenreTracks.Any())
            .OrderByDescending(keySelector: genre => genre.MusicGenreTracks.Count())
            .ThenBy(keySelector: genre => genre.Id)
            .Select(selector: genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count(),
            });
    }

    private static IQueryable<AlbumCardDto> GetLatestAlbumCardsQuery(MediaContext ctx)
    {
        return ctx
            .Albums.AsNoTracking()
            .Where(predicate: album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .OrderByDescending(keySelector: album => album.CreatedAt)
            .ThenBy(keySelector: album => album.Id)
            .Select(selector: album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette ?? string.Empty,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(),
            });
    }

    #endregion
}
