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
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        int result = await mediaContext
            .Artists.Where(artist => artist.Id == id)
            .ExecuteDeleteAsync(ct);
        return result > 0;
    }

    public async Task<Artist?> GetArtistByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .FirstOrDefaultAsync(artist => artist.Id == id, ct);
    }

    public async Task<Album?> GetAlbumByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .FirstOrDefaultAsync(album => album.Id == id, ct);
    }

    public async Task<Artist?> GetArtistForEditAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .FirstOrDefaultAsync(artist => artist.Id == id, ct);
    }

    public async Task<Artist?> GetArtistWithLibraryFolderAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Include(artist => artist.LibraryFolder)
                .ThenInclude(folder => folder.Driver)
            .FirstOrDefaultAsync(artist => artist.Id == id, ct);
    }

    public async Task<Album?> GetAlbumForEditAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .FirstOrDefaultAsync(album => album.Id == id, ct);
    }

    public async Task<Album?> GetAlbumWithLibraryFolderAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Include(album => album.LibraryFolder)
                .ThenInclude(folder => folder.Driver)
            .FirstOrDefaultAsync(album => album.Id == id, ct);
    }

    public async Task<bool> PlaylistNameExistsAsync(
        string name,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext.Playlists.AnyAsync(
            playlist => playlist.Name == name && playlist.UserId == userId,
            ct
        );
    }

    public async Task CreatePlaylistAsync(
        Playlist playlist,
        List<Guid> trackIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        mediaContext.Playlists.Add(playlist);

        foreach (Guid trackId in trackIds)
            mediaContext.PlaylistTrack.Add(new() { PlaylistId = playlist.Id, TrackId = trackId });

        await mediaContext.SaveChangesAsync(ct);
    }

    public async Task<Playlist?> GetPlaylistByNameAsync(
        string name,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Include(playlist => playlist.Tracks)
                .ThenInclude(playlistTrack => playlistTrack.Track)
            .FirstOrDefaultAsync(
                playlist => playlist.Name == name && playlist.UserId == userId,
                ct
            );
    }

    public async Task<Playlist?> GetPlaylistForEditAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .FirstOrDefaultAsync(playlist => playlist.Id == id, ct);
    }

    public async Task<Playlist?> GetPlaylistForCoverAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .FirstOrDefaultAsync(playlist => playlist.Id == id, ct);
    }

    public async Task<int> DeletePlaylistAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        return await mediaContext
            .Playlists.Where(playlist => playlist.Id == id && playlist.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> AddPlaylistTrackAsync(
        Guid playlistId,
        Guid trackId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        mediaContext.PlaylistTrack.Add(new() { PlaylistId = playlistId, TrackId = trackId });
        return await mediaContext.SaveChangesAsync(ct);
    }

    public async Task<int> RemovePlaylistTrackAsync(
        Guid playlistId,
        Guid trackId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        PlaylistTrack? playlistTrack = await mediaContext
            .PlaylistTrack.Where(pt => pt.Playlist.UserId == userId)
            .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId, ct);

        if (playlistTrack is null)
            return -1;

        mediaContext.PlaylistTrack.Remove(playlistTrack);
        return await mediaContext.SaveChangesAsync(ct);
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
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Artist? artist = await mediaContext.Artists.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (artist is null)
            return 0;

        artist.Name = name;
        artist.Description = description;
        artist.Cover = cover;
        artist._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(ct);
    }

    public async Task UpdateArtistCoverAsync(
        Guid id,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Artist? artist = await mediaContext.Artists.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (artist is null)
            return;

        artist.Cover = cover;
        artist._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(ct);
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
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Album? album = await mediaContext.Albums.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (album is null)
            return 0;

        album.Name = name;
        album.Description = description;
        album.Cover = cover;
        album._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAlbumCoverAsync(
        Guid id,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Album? album = await mediaContext.Albums.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (album is null)
            return;

        album.Cover = cover;
        album._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(ct);
    }

    public async Task<int> UpdatePlaylistMetadataAsync(
        Guid id,
        string name,
        string? description,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Playlist? playlist = await mediaContext.Playlists.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist is null)
            return 0;

        playlist.Name = name;
        playlist.Description = description;
        playlist.Cover = cover;
        playlist._colorPalette = colorPalette;

        return await mediaContext.SaveChangesAsync(ct);
    }

    public async Task UpdatePlaylistCoverAsync(
        Guid id,
        Guid userId,
        string cover,
        string colorPalette,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(ct);
        Playlist? playlist = await mediaContext
            .Playlists.Where(p => p.UserId == userId)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist is null)
            return;

        playlist.Cover = cover;
        playlist._colorPalette = colorPalette;

        await mediaContext.SaveChangesAsync(ct);
    }

    #endregion
}
