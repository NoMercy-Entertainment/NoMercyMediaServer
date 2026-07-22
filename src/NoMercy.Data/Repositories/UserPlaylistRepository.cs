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
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        UserPlaylist playlist = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Cover = cover,
            UserId = userId,
        };

        context.UserPlaylists.Add(entity: playlist);
        await context.SaveChangesAsync(cancellationToken: ct);

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
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        bool ownsPlaylist = await context.UserPlaylists.AnyAsync(
            predicate: p => p.Id == playlistId && p.UserId == userId,
            cancellationToken: ct
        );
        if (!ownsPlaylist)
            return null;

        if (!await MediaExistsAsync(context: context, item: item, ct: ct))
            return null;

        int targetOrder;
        if (order.HasValue)
        {
            targetOrder = order.Value;
            // Make room at the insertion point instead of colliding with it —
            // every existing item at or past the target shifts down by one.
            await context
                .PlaylistItems.Where(predicate: pi =>
                    pi.UserPlaylistId == playlistId && pi.Order >= targetOrder
                )
                .ExecuteUpdateAsync(
                    setPropertyCalls: setters => setters.SetProperty(propertyExpression: pi => pi.Order, valueExpression: pi => pi.Order + 1),
                    cancellationToken: ct
                );
        }
        else
        {
            int? maxOrder = await context
                .PlaylistItems.Where(predicate: pi => pi.UserPlaylistId == playlistId)
                .Select(selector: pi => (int?)pi.Order)
                .MaxAsync(cancellationToken: ct);
            targetOrder = (maxOrder ?? -1) + 1;
        }

        PlaylistItem playlistItem = new()
        {
            Id = Ulid.NewUlid(),
            UserPlaylistId = playlistId,
            Kind = item.Kind,
            Order = targetOrder,
            MovieId = item.MovieId,
            TvId = item.TvId,
            EpisodeId = item.EpisodeId,
            SpecialId = item.SpecialId,
        };

        context.PlaylistItems.Add(entity: playlistItem);
        await context.SaveChangesAsync(cancellationToken: ct);

        return playlistItem;
    }

    private static Task<bool> MediaExistsAsync(
        MediaContext context,
        PlaylistItemRef item,
        CancellationToken ct
    ) =>
        item.Kind switch
        {
            PlaylistItemKind.Movie => context.Movies.AnyAsync(predicate: m => m.Id == item.MovieId, cancellationToken: ct),
            PlaylistItemKind.Tv => context.Tvs.AnyAsync(predicate: t => t.Id == item.TvId, cancellationToken: ct),
            PlaylistItemKind.Episode => context.Episodes.AnyAsync(predicate: e => e.Id == item.EpisodeId, cancellationToken: ct),
            PlaylistItemKind.Special => context.Specials.AnyAsync(predicate: s => s.Id == item.SpecialId, cancellationToken: ct),
            _ => Task.FromResult(result: false),
        };

    public async Task<bool> RemoveItemAsync(
        Guid playlistId,
        Guid userId,
        Ulid itemId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        PlaylistItem? item = await context
            .PlaylistItems.Where(predicate: pi =>
                pi.Id == itemId
                && pi.UserPlaylistId == playlistId
                && pi.UserPlaylist.UserId == userId
            )
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (item is null)
            return false;

        context.PlaylistItems.Remove(entity: item);
        await context.SaveChangesAsync(cancellationToken: ct);

        return true;
    }

    public async Task<bool> ReorderAsync(
        Guid playlistId,
        Guid userId,
        IReadOnlyList<Ulid> orderedItemIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        bool ownsPlaylist = await context.UserPlaylists.AnyAsync(
            predicate: p => p.Id == playlistId && p.UserId == userId,
            cancellationToken: ct
        );
        if (!ownsPlaylist)
            return false;

        List<PlaylistItem> items = await context
            .PlaylistItems.Where(predicate: pi => pi.UserPlaylistId == playlistId)
            .ToListAsync(cancellationToken: ct);

        // The caller must supply exactly the current item set — a partial or
        // stale list is rejected outright rather than silently reordering (and
        // implicitly dropping) a subset.
        HashSet<Ulid> currentIds = items.Select(selector: i => i.Id).ToHashSet();
        HashSet<Ulid> requestedIds = orderedItemIds.ToHashSet();
        if (currentIds.Count != requestedIds.Count || !currentIds.SetEquals(other: requestedIds))
            return false;

        Dictionary<Ulid, PlaylistItem> byId = items.ToDictionary(keySelector: i => i.Id);
        for (int index = 0; index < orderedItemIds.Count; index++)
            byId[key: orderedItemIds[index: index]].Order = index;

        await context.SaveChangesAsync(cancellationToken: ct);

        return true;
    }

    public async Task<List<UserPlaylistSummary>> GetUserPlaylistsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<UserPlaylist> playlists = await context
            .UserPlaylists.AsNoTracking()
            .Where(predicate: p => p.UserId == userId)
            .ToListAsync(cancellationToken: ct);

        if (playlists.Count == 0)
            return [];

        // Second, flat query — UserPlaylist deliberately carries no PlaylistItems
        // collection navigation, so the per-playlist count is fetched separately
        // and merged in memory rather than through a correlated subquery.
        List<Guid> playlistIds = playlists.Select(selector: p => p.Id).ToList();
        Dictionary<Guid, int> itemCounts = await context
            .PlaylistItems.AsNoTracking()
            .Where(predicate: pi => playlistIds.Contains(pi.UserPlaylistId))
            .GroupBy(keySelector: pi => pi.UserPlaylistId)
            .Select(selector: g => new { UserPlaylistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(keySelector: x => x.UserPlaylistId, elementSelector: x => x.Count, cancellationToken: ct);

        return playlists
            .Select(selector: p => new UserPlaylistSummary(
                Id: p.Id,
                Name: p.Name,
                Cover: p.Cover,
                ItemCount: itemCounts.GetValueOrDefault(key: p.Id, defaultValue: 0)
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
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        bool ownsPlaylist = await context.UserPlaylists.AnyAsync(
            predicate: p => p.Id == playlistId && p.UserId == userId,
            cancellationToken: ct
        );
        if (!ownsPlaylist)
            return null;

        // Rooted at PlaylistItem, never at UserPlaylist/Movie/Tv/Episode/Special —
        // none of those four carry a collection navigation back to PlaylistItem (by
        // design, see PlaylistItem.cs), so this AsNoTracking Include tree can't hit
        // EF Core's no-tracking Include-cycle validator the way the music playlist
        // read path once did.
        return await context
            .PlaylistItems.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: pi => pi.UserPlaylistId == playlistId)
            .Include(navigationPropertyPath: pi => pi.Movie)
                .ThenInclude(navigationPropertyPath: m => m!.Images.Where(i => i.Type == "poster" || i.Type == "logo"))
            .Include(navigationPropertyPath: pi => pi.Movie)
                .ThenInclude(navigationPropertyPath: m => m!.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: pi => pi.Movie)
                .ThenInclude(navigationPropertyPath: m => m!.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: pi => pi.Movie)
                .ThenInclude(navigationPropertyPath: m =>
                    m!
                        .CertificationMovies.Where(c =>
                            c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                        )
                        .OrderBy(c => c.CertificationId)
                        .Take(1)
                )
                    .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: pi => pi.Tv)
                .ThenInclude(navigationPropertyPath: tv => tv!.Images.Where(i => i.Type == "poster" || i.Type == "logo"))
            .Include(navigationPropertyPath: pi => pi.Tv)
                .ThenInclude(navigationPropertyPath: tv => tv!.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: pi => pi.Episode)
                .ThenInclude(navigationPropertyPath: e => e!.Tv)
            .Include(navigationPropertyPath: pi => pi.Episode)
                .ThenInclude(navigationPropertyPath: e => e!.Images.Where(i => i.Type == "still"))
            .Include(navigationPropertyPath: pi => pi.Episode)
                .ThenInclude(navigationPropertyPath: e => e!.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: pi => pi.Episode)
                .ThenInclude(navigationPropertyPath: e => e!.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: pi => pi.Special)
            .OrderBy(keySelector: pi => pi.Order)
            .ThenBy(keySelector: pi => pi.Id)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<bool> OwnsPlaylistAsync(
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        return await context.UserPlaylists.AnyAsync(
            predicate: p => p.Id == playlistId && p.UserId == userId,
            cancellationToken: ct
        );
    }

    public async Task<UserPlaylistDetail?> GetPlaylistAsync(
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        UserPlaylist? playlist = await context
            .UserPlaylists.AsNoTracking()
            .Where(predicate: p => p.Id == playlistId && p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return playlist is null
            ? null
            : new UserPlaylistDetail(
                Id: playlist.Id,
                Name: playlist.Name,
                Description: playlist.Description,
                Cover: playlist.Cover
            );
    }

    public async Task<bool> UpdatePlaylistAsync(
        Guid playlistId,
        Guid userId,
        string? name,
        string? description,
        string? cover,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        UserPlaylist? playlist = await context
            .UserPlaylists.Where(predicate: p => p.Id == playlistId && p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (playlist is null)
            return false;

        if (name is not null)
            playlist.Name = name;
        if (description is not null)
            playlist.Description = description;
        if (cover is not null)
            playlist.Cover = cover;

        await context.SaveChangesAsync(cancellationToken: ct);

        return true;
    }

    public async Task<bool> DeletePlaylistAsync(
        Guid playlistId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        // PlaylistItems cascade-delete via FK_PlaylistItems_UserPlaylists_UserPlaylistId
        // ON DELETE CASCADE (see CorrectUserPlaylistToVideoOnlyContainer migration) —
        // the same ExecuteDeleteAsync-on-parent pattern MusicRepository.DeletePlaylistAsync
        // already relies on for the legacy music playlist's PlaylistTrack children.
        int deleted = await context
            .UserPlaylists.Where(predicate: p => p.Id == playlistId && p.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken: ct);

        return deleted > 0;
    }
}
