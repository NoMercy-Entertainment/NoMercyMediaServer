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
    #region Mutations — Artist / Album / Playlist

    public async Task<bool> DeleteArtistAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        int result = await mediaContext
            .Artists.Where(predicate: artist => artist.Id == id)
            .ExecuteDeleteAsync(cancellationToken: ct);
        return result > 0;
    }

    public async Task<Artist?> GetArtistByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .FirstOrDefaultAsync(predicate: artist => artist.Id == id, cancellationToken: ct);
    }

    public async Task<Album?> GetAlbumByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .FirstOrDefaultAsync(predicate: album => album.Id == id, cancellationToken: ct);
    }

    public async Task<Artist?> GetArtistForEditAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .FirstOrDefaultAsync(predicate: artist => artist.Id == id, cancellationToken: ct);
    }

    public async Task<Artist?> GetArtistWithLibraryFolderAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Include(navigationPropertyPath: artist => artist.LibraryFolder)
                .ThenInclude(navigationPropertyPath: folder => folder.Driver)
            .FirstOrDefaultAsync(predicate: artist => artist.Id == id, cancellationToken: ct);
    }

    public async Task<Album?> GetAlbumForEditAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .FirstOrDefaultAsync(predicate: album => album.Id == id, cancellationToken: ct);
    }

    public async Task<Album?> GetAlbumWithLibraryFolderAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Include(navigationPropertyPath: album => album.LibraryFolder)
                .ThenInclude(navigationPropertyPath: folder => folder.Driver)
            .FirstOrDefaultAsync(predicate: album => album.Id == id, cancellationToken: ct);
    }

    public async Task<bool> PlaylistNameExistsAsync(
        string name,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext.Playlists.AnyAsync(
            predicate: playlist => playlist.Name == name && playlist.UserId == userId,
            cancellationToken: ct
        );
    }

    public async Task CreatePlaylistAsync(
        Playlist playlist,
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        mediaContext.Playlists.Add(entity: playlist);

        foreach (Guid trackId in trackIds)
            mediaContext.PlaylistTrack.Add(entity: new() { PlaylistId = playlist.Id, TrackId = trackId });

        await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<Playlist?> GetPlaylistByNameAsync(
        string name,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Include(navigationPropertyPath: playlist => playlist.Tracks)
                .ThenInclude(navigationPropertyPath: playlistTrack => playlistTrack.Track)
            .FirstOrDefaultAsync(
                predicate: playlist => playlist.Name == name && playlist.UserId == userId,
                cancellationToken: ct
            );
    }

    public async Task<Playlist?> GetPlaylistForEditAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .FirstOrDefaultAsync(predicate: playlist => playlist.Id == id, cancellationToken: ct);
    }

    public async Task<Playlist?> GetPlaylistForCoverAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .FirstOrDefaultAsync(predicate: playlist => playlist.Id == id, cancellationToken: ct);
    }

    public async Task<int> DeletePlaylistAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.Where(predicate: playlist => playlist.Id == id && playlist.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken: ct);
    }

    public async Task<int> AddPlaylistTrackAsync(
        Guid playlistId,
        Guid trackId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        bool ownsPlaylist = await mediaContext.Playlists.AnyAsync(
            predicate: playlist => playlist.Id == playlistId && playlist.UserId == userId,
            cancellationToken: ct
        );
        if (!ownsPlaylist)
            return -1;

        mediaContext.PlaylistTrack.Add(entity: new() { PlaylistId = playlistId, TrackId = trackId });
        return await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<int> RemovePlaylistTrackAsync(
        Guid playlistId,
        Guid trackId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        PlaylistTrack? playlistTrack = await mediaContext
            .PlaylistTrack.Where(predicate: pt => pt.Playlist.UserId == userId)
            .FirstOrDefaultAsync(predicate: pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, cancellationToken: ct);

        if (playlistTrack is null)
            return -1;

        mediaContext.PlaylistTrack.Remove(entity: playlistTrack);
        return await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<int> UpdateArtistMetadataAsync(
        Guid id,
        string name,
        string? description,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Artist? artist = await mediaContext.Artists.FirstOrDefaultAsync(predicate: a => a.Id == id, cancellationToken: ct);
        if (artist is null)
            return 0;

        artist.Name = name;
        artist.Description = description;
        artist.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        artist._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task UpdateArtistCoverAsync(
        Guid id,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Artist? artist = await mediaContext.Artists.FirstOrDefaultAsync(predicate: a => a.Id == id, cancellationToken: ct);
        if (artist is null)
            return;

        artist.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        artist._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<int> UpdateAlbumMetadataAsync(
        Guid id,
        string name,
        string? description,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Album? album = await mediaContext.Albums.FirstOrDefaultAsync(predicate: a => a.Id == id, cancellationToken: ct);
        if (album is null)
            return 0;

        album.Name = name;
        album.Description = description;
        album.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        album._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task UpdateAlbumCoverAsync(
        Guid id,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Album? album = await mediaContext.Albums.FirstOrDefaultAsync(predicate: a => a.Id == id, cancellationToken: ct);
        if (album is null)
            return;

        album.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        album._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<int> UpdatePlaylistMetadataAsync(
        Guid id,
        Guid userId,
        string name,
        string? description,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Playlist? playlist = await mediaContext
            .Playlists.Where(predicate: p => p.UserId == userId)
            .FirstOrDefaultAsync(predicate: p => p.Id == id, cancellationToken: ct);
        if (playlist is null)
            return 0;

        playlist.Name = name;
        playlist.Description = description;
        playlist.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        playlist._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task UpdatePlaylistCoverAsync(
        Guid id,
        Guid userId,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Playlist? playlist = await mediaContext
            .Playlists.Where(predicate: p => p.UserId == userId)
            .FirstOrDefaultAsync(predicate: p => p.Id == id, cancellationToken: ct);
        if (playlist is null)
            return;

        playlist.Cover = string.IsNullOrEmpty(value: cover) ? null : cover;
        playlist._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    #endregion
}
