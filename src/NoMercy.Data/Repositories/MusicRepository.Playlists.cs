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
using NoMercy.Data.DTOs;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public partial class MusicRepository
{
    #region Playlist Queries

    public async Task<List<CarouselResponseItemDto>> GetCarouselPlaylistsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Include(playlist => playlist.Tracks)
                .ThenInclude(trackUser => trackUser.Track)
            .Select(playlist => new CarouselResponseItemDto(playlist))
            .Take(36)
            .ToListAsync(ct);
    }

    public async Task<Playlist?> GetPlaylistAsync(
        Guid userId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
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

    #region Playlist Management

    public async Task<PlaylistTrack?> GetPlaylistTrackAsync(
        Guid userId,
        Guid playlistId,
        Guid trackId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .PlaylistTrack.AsNoTracking()
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

    public async Task<AlbumTrack?> GetAlbumTrackAsync(
        Guid userId,
        Guid albumId,
        Guid trackId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .AlbumTrack.Where(at => at.AlbumId == albumId && at.TrackId == trackId)
            .Include(at => at.Track)
            .Include(at => at.Album)
                .ThenInclude(album =>
                    album
                        .AlbumTrack.OrderBy(albumTrack => albumTrack.Track.DiscNumber)
                        .ThenBy(albumTrack => albumTrack.Track.TrackNumber)
                )
                    .ThenInclude(albumTrack => albumTrack.Track)
                        .ThenInclude(track => track.ArtistTrack)
                            .ThenInclude(artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ArtistTrack?> GetArtistTrackAsync(
        Guid userId,
        Guid artistId,
        Guid trackId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .ArtistTrack.Where(at => at.ArtistId == artistId && at.TrackId == trackId)
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

    public async Task<MusicGenreTrack?> GetGenreTrackAsync(
        Guid userId,
        Guid genreId,
        Guid trackId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .MusicGenreTrack.Where(genre =>
                genre.Genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.Genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
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

    #region Projection Methods — Playlist Cards

    public async Task<List<PlaylistCardDto>> GetPlaylistCardsAsync(
        Guid userId,
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .Select(playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette ?? string.Empty,
                TrackCount = playlist.Tracks.Count(),
            })
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<PlaylistCardDto>> GetPlaylistCardsByIdsAsync(
        List<Guid> playlistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(playlist => playlistIds.Contains(playlist.Id))
            .Select(playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette ?? string.Empty,
                TrackCount = playlist.Tracks.Count(),
            })
            .ToListAsync(ct);
    }

    #endregion
}
