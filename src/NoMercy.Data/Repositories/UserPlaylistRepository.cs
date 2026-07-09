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
using NoMercy.Database.Models.Playlists;

namespace NoMercy.Data.Repositories;

public class UserPlaylistRepository(IDbContextFactory<MediaContext> contextFactory)
    : IUserPlaylistRepository
{
    public async Task<Guid> CreatePlaylistAsync(
        Guid userId,
        string name,
        string? description = null,
        string? cover = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        Playlist playlist = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Cover = cover,
            UserId = userId,
        };

        context.Playlists.Add(playlist);
        await context.SaveChangesAsync(ct);

        return playlist.Id;
    }

    public async Task<PlaylistItem?> AddItemAsync(
        Guid playlistId,
        Guid userId,
        PlaylistItemRef item,
        int? order = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        bool ownsPlaylist = await context.Playlists.AnyAsync(
            p => p.Id == playlistId && p.UserId == userId,
            ct
        );
        if (!ownsPlaylist)
            return null;

        if (!await MediaExistsAsync(context, item, ct))
            return null;

        int targetOrder;
        if (order.HasValue)
        {
            targetOrder = order.Value;
            // Make room at the insertion point instead of colliding with it —
            // every existing item at or past the target shifts down by one.
            await context
                .PlaylistItems.Where(pi => pi.PlaylistId == playlistId && pi.Order >= targetOrder)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(pi => pi.Order, pi => pi.Order + 1),
                    ct
                );
        }
        else
        {
            int? maxOrder = await context
                .PlaylistItems.Where(pi => pi.PlaylistId == playlistId)
                .Select(pi => (int?)pi.Order)
                .MaxAsync(ct);
            targetOrder = (maxOrder ?? -1) + 1;
        }

        PlaylistItem playlistItem = new()
        {
            Id = Ulid.NewUlid(),
            PlaylistId = playlistId,
            Kind = item.Kind,
            Order = targetOrder,
            MovieId = item.MovieId,
            TvId = item.TvId,
            EpisodeId = item.EpisodeId,
            TrackId = item.TrackId,
            SpecialId = item.SpecialId,
        };

        context.PlaylistItems.Add(playlistItem);
        await context.SaveChangesAsync(ct);

        return playlistItem;
    }

    private static Task<bool> MediaExistsAsync(
        MediaContext context,
        PlaylistItemRef item,
        CancellationToken ct
    ) =>
        item.Kind switch
        {
            PlaylistItemKind.Movie => context.Movies.AnyAsync(m => m.Id == item.MovieId, ct),
            PlaylistItemKind.Tv => context.Tvs.AnyAsync(t => t.Id == item.TvId, ct),
            PlaylistItemKind.Episode => context.Episodes.AnyAsync(e => e.Id == item.EpisodeId, ct),
            PlaylistItemKind.Track => context.Tracks.AnyAsync(t => t.Id == item.TrackId, ct),
            PlaylistItemKind.Special => context.Specials.AnyAsync(s => s.Id == item.SpecialId, ct),
            _ => Task.FromResult(false),
        };

    public async Task<bool> RemoveItemAsync(
        Guid playlistId,
        Guid userId,
        Ulid itemId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        PlaylistItem? item = await context
            .PlaylistItems.Where(pi =>
                pi.Id == itemId && pi.PlaylistId == playlistId && pi.Playlist.UserId == userId
            )
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return false;

        context.PlaylistItems.Remove(item);
        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> ReorderAsync(
        Guid playlistId,
        Guid userId,
        IReadOnlyList<Ulid> orderedItemIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        bool ownsPlaylist = await context.Playlists.AnyAsync(
            p => p.Id == playlistId && p.UserId == userId,
            ct
        );
        if (!ownsPlaylist)
            return false;

        List<PlaylistItem> items = await context
            .PlaylistItems.Where(pi => pi.PlaylistId == playlistId)
            .ToListAsync(ct);

        // The caller must supply exactly the current item set — a partial or
        // stale list is rejected outright rather than silently reordering (and
        // implicitly dropping) a subset.
        HashSet<Ulid> currentIds = items.Select(i => i.Id).ToHashSet();
        HashSet<Ulid> requestedIds = orderedItemIds.ToHashSet();
        if (currentIds.Count != requestedIds.Count || !currentIds.SetEquals(requestedIds))
            return false;

        Dictionary<Ulid, PlaylistItem> byId = items.ToDictionary(i => i.Id);
        for (int index = 0; index < orderedItemIds.Count; index++)
            byId[orderedItemIds[index]].Order = index;

        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<List<UserPlaylistSummary>> GetUserPlaylistsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        List<Playlist> playlists = await context
            .Playlists.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        if (playlists.Count == 0)
            return [];

        // Second, flat query — Playlist deliberately carries no PlaylistItems
        // collection navigation (kept untouched for the legacy music path), so
        // the per-playlist count is fetched separately and merged in memory
        // rather than through a correlated subquery.
        List<Guid> playlistIds = playlists.Select(p => p.Id).ToList();
        Dictionary<Guid, int> itemCounts = await context
            .PlaylistItems.AsNoTracking()
            .Where(pi => playlistIds.Contains(pi.PlaylistId))
            .GroupBy(pi => pi.PlaylistId)
            .Select(g => new { PlaylistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlaylistId, x => x.Count, ct);

        return playlists
            .Select(p => new UserPlaylistSummary(
                p.Id,
                p.Name,
                p.Cover,
                itemCounts.GetValueOrDefault(p.Id, 0)
            ))
            .ToList();
    }

    public async Task<List<PlaylistItem>?> GetPlaylistItemsAsync(
        Guid playlistId,
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        bool ownsPlaylist = await context.Playlists.AnyAsync(
            p => p.Id == playlistId && p.UserId == userId,
            ct
        );
        if (!ownsPlaylist)
            return null;

        // Rooted at PlaylistItem, never at Playlist/Movie/Tv/Episode/Track/Special —
        // none of those five carry a collection navigation back to PlaylistItem (by
        // design, see PlaylistItem.cs), so this AsNoTracking Include tree can't hit
        // EF Core's no-tracking Include-cycle validator the way the music playlist
        // read path once did.
        return await context
            .PlaylistItems.AsNoTracking()
            .AsSplitQuery()
            .Where(pi => pi.PlaylistId == playlistId)
            .Include(pi => pi.Movie)
                .ThenInclude(m => m!.Images.Where(i => i.Type == "poster" || i.Type == "logo"))
            .Include(pi => pi.Movie)
                .ThenInclude(m => m!.Translations.Where(t => t.Iso6391 == language))
            .Include(pi => pi.Movie)
                .ThenInclude(m => m!.VideoFiles.Where(v => v.Folder != null))
            .Include(pi => pi.Movie)
                .ThenInclude(m =>
                    m!
                        .CertificationMovies.Where(c =>
                            c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                        )
                        .Take(1)
                )
                    .ThenInclude(c => c.Certification)
            .Include(pi => pi.Tv)
                .ThenInclude(tv => tv!.Images.Where(i => i.Type == "poster" || i.Type == "logo"))
            .Include(pi => pi.Tv)
                .ThenInclude(tv => tv!.Translations.Where(t => t.Iso6391 == language))
            .Include(pi => pi.Episode)
                .ThenInclude(e => e!.Tv)
            .Include(pi => pi.Episode)
                .ThenInclude(e => e!.Images.Where(i => i.Type == "still"))
            .Include(pi => pi.Episode)
                .ThenInclude(e => e!.Translations.Where(t => t.Iso6391 == language))
            .Include(pi => pi.Episode)
                .ThenInclude(e => e!.VideoFiles.Where(v => v.Folder != null))
            .Include(pi => pi.Track)
                .ThenInclude(t => t!.AlbumTrack)
                    .ThenInclude(at => at.Album)
            .Include(pi => pi.Track)
                .ThenInclude(t => t!.ArtistTrack)
                    .ThenInclude(at => at.Artist)
            .Include(pi => pi.Special)
            .OrderBy(pi => pi.Order)
            .ToListAsync(ct);
    }
}
