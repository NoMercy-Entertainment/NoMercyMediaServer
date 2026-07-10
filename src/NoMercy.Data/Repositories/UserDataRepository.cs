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
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Data.Repositories;

public class UserDataRepository(IDbContextFactory<MediaContext> contextFactory)
    : IUserDataRepository
{
    public async Task<List<UserData>> GetUserDataAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        IQueryable<UserData>? query = BuildQuery(context, userId, type, intId, ulidId);
        return query is null ? [] : await query.ToListAsync(ct);
    }

    public async Task<UserData?> GetUserDataSingleAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        IQueryable<UserData>? query = BuildQuery(context, userId, type, intId, ulidId);
        return query is null ? null : await query.FirstOrDefaultAsync(ct);
    }

    public async Task<int> DeleteUserDataAsync(
        List<UserData> userData,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        context.UserData.RemoveRange(userData);
        return await context.SaveChangesAsync(ct);
    }

    public async Task<int> HideFromContinueWatchingAsync(
        IEnumerable<UserData> userData,
        CancellationToken ct = default
    )
    {
        List<Ulid> ids = userData.Select(data => data.Id).ToList();
        if (ids.Count == 0)
            return 0;

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        return await context
            .UserData.Where(data => ids.Contains(data.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(data => data.RemovedFromContinueWatching, true),
                ct
            );
    }

    private IQueryable<UserData>? BuildQuery(
        MediaContext context,
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId
    )
    {
        IQueryable<UserData> query = context
            .UserData.AsNoTracking()
            .Where(data => data.UserId.Equals(userId));

        return type switch
        {
            MediaTypes.MovieMediaType => query.Where(data => data.MovieId == intId),
            MediaTypes.TvMediaType => query.Where(data => data.TvId == intId),
            MediaTypes.SpecialMediaType => query.Where(data => data.SpecialId == ulidId),
            MediaTypes.CollectionMediaType => query.Where(data => data.CollectionId == intId),
            _ => null,
        };
    }
}
