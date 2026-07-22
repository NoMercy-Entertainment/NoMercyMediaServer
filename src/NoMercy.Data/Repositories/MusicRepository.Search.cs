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
    #region Search Operations

    public async Task<List<Guid>> SearchArtistIdsAsync(
        string normalizedQuery,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Where(predicate: artist => MediaContext.NormalizeSearch(artist.Name).Contains(normalizedQuery))
            .OrderBy(keySelector: artist => artist.Name)
            .ThenBy(keySelector: artist => artist.Id)
            .Select(selector: artist => artist.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> SearchAlbumIdsAsync(
        string normalizedQuery,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => MediaContext.NormalizeSearch(album.Name).Contains(normalizedQuery))
            .OrderBy(keySelector: album => album.Name)
            .ThenBy(keySelector: album => album.Id)
            .Select(selector: album => album.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> SearchPlaylistIdsAsync(
        string normalizedQuery,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist =>
                MediaContext.NormalizeSearch(playlist.Name).Contains(normalizedQuery)
            )
            .OrderBy(keySelector: playlist => playlist.Name)
            .ThenBy(keySelector: playlist => playlist.Id)
            .Select(selector: playlist => playlist.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> SearchTrackIdsAsync(
        string normalizedQuery,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => MediaContext.NormalizeSearch(track.Name).Contains(normalizedQuery))
            .OrderBy(keySelector: track => track.Name)
            .ThenBy(keySelector: track => track.Id)
            .Select(selector: track => track.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Artist>> GetArtistsByIdsAsync(
        List<Guid> artistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Where(predicate: artist => artistIds.Contains(artist.Id))
            .OrderBy(keySelector: artist => artist.Name)
            .ThenBy(keySelector: artist => artist.Id)
            .Include(navigationPropertyPath: artist => artist.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Track)
            .Include(navigationPropertyPath: artist => artist.AlbumArtist)
                .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Album)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Album>> GetAlbumsByIdsAsync(
        List<Guid> albumIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => albumIds.Contains(album.Id))
            .OrderBy(keySelector: album => album.Name)
            .ThenBy(keySelector: album => album.Id)
            .Include(navigationPropertyPath: album => album.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Track)
                    .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                        .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: album => album.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Track)
                    .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Playlist>> GetPlaylistsByIdsAsync(
        List<Guid> playlistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlistIds.Contains(playlist.Id))
            .OrderBy(keySelector: playlist => playlist.Name)
            .ThenBy(keySelector: playlist => playlist.Id)
            .Include(navigationPropertyPath: playlist => playlist.Tracks)
                .ThenInclude(navigationPropertyPath: playlistTrack => playlistTrack.Track)
                    .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Track>> GetTracksByIdsAsync(
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => trackIds.Contains(track.Id))
            .OrderBy(keySelector: track => track.Name)
            .ThenBy(keySelector: track => track.Id)
            .Include(navigationPropertyPath: track => track.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: track => track.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .Include(navigationPropertyPath: track => track.PlaylistTrack)
                .ThenInclude(navigationPropertyPath: playlistTrack => playlistTrack.Playlist)
            .Include(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Projection Methods — Search Cross-Reference

    public async Task<List<Guid>> GetArtistIdsFromAlbumsAsync(
        List<Guid> albumIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .AlbumTrack.AsNoTracking()
            .Where(predicate: at => albumIds.Contains(at.AlbumId))
            .SelectMany(selector: at => at.Track.ArtistTrack)
            .Select(selector: at => at.ArtistId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> GetArtistIdsFromPlaylistTracksAsync(
        List<Guid> playlistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .PlaylistTrack.AsNoTracking()
            .Where(predicate: pt => playlistIds.Contains(pt.PlaylistId))
            .SelectMany(selector: pt => pt.Track.ArtistTrack)
            .Select(selector: at => at.ArtistId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> GetArtistIdsFromTracksAsync(
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .ArtistTrack.AsNoTracking()
            .Where(predicate: at => trackIds.Contains(at.TrackId))
            .Select(selector: at => at.ArtistId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Guid>> GetAlbumIdsFromTracksAsync(
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .AlbumTrack.AsNoTracking()
            .Where(predicate: at => trackIds.Contains(at.TrackId))
            .Select(selector: at => at.AlbumId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<SearchTrackCardDto>> SearchTrackCardsAsync(
        List<Guid> trackIds,
        Guid userId,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => trackIds.Contains(track.Id))
            .Select(selector: track => new SearchTrackCardDto
            {
                Id = track.Id,
                Name = track.Name,
                FolderId = track.FolderId,
                Folder = track.Folder,
                Filename = track.Filename,
                Cover = track.Cover,
                ColorPalette = track._colorPalette ?? string.Empty,
                Duration = track.Duration,
                DiscNumber = track.DiscNumber,
                TrackNumber = track.TrackNumber,
                Quality = track.Quality,
                UpdatedAt = track.UpdatedAt,
                Favorite = track.TrackUser.Any(tu => tu.UserId == userId),
                AlbumId = track.AlbumTrack.Select(at => at.AlbumId.ToString()).FirstOrDefault(),
                AlbumName = track.AlbumTrack.Select(at => at.Album.Name).FirstOrDefault(),
                AlbumCover = track.AlbumTrack.Select(at => at.Album.Cover).FirstOrDefault(),
                AlbumColorPalette = track
                    .AlbumTrack.Select(at => at.Album._colorPalette)
                    .FirstOrDefault(),
                ArtistCover = track.ArtistTrack.Select(at => at.Artist.Cover).FirstOrDefault(),
                ArtistColorPalette = track
                    .ArtistTrack.Select(at => at.Artist._colorPalette)
                    .FirstOrDefault(),
                Artists = track
                    .ArtistTrack.Select(at => new SearchTrackArtistDto
                    {
                        Id = at.ArtistId,
                        Name = at.Artist.Name,
                    })
                    .ToList(),
                Albums = track
                    .AlbumTrack.Select(at => new SearchTrackAlbumDto
                    {
                        Id = at.AlbumId,
                        Name = at.Album.Name,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Search — Parallel Full-Data Fetch

    public async Task<MusicSearchFullData> SearchMusicFullDataAsync(
        List<Guid> artistIds,
        List<Guid> albumIds,
        List<Guid> playlistIds,
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        // Each entity set is fetched via its own repository method (which manages its own DbContext)
        // so the four queries run in parallel without sharing a (non-thread-safe) context.
        Task<List<Artist>> artistsTask = Task.Run(
            function: async () => await GetArtistsByIdsAsync(artistIds: artistIds, ct: ct),
            cancellationToken: ct
        );

        Task<List<Album>> albumsTask = Task.Run(
            function: async () => await GetAlbumsByIdsAsync(albumIds: albumIds, ct: ct),
            cancellationToken: ct
        );

        Task<List<Playlist>> playlistsTask = Task.Run(
            function: async () => await GetPlaylistsByIdsAsync(playlistIds: playlistIds, ct: ct),
            cancellationToken: ct
        );

        Task<List<Track>> tracksTask = Task.Run(
            function: async () => await GetTracksByIdsAsync(trackIds: trackIds, ct: ct),
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [artistsTask, albumsTask, playlistsTask, tracksTask]);

        return new(Artists: artistsTask.Result, Albums: albumsTask.Result, Playlists: playlistsTask.Result, Songs: tracksTask.Result);
    }

    #endregion
}
