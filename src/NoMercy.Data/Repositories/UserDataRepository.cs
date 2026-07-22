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
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        IQueryable<UserData>? query = BuildQuery(context: context, userId: userId, type: type, intId: intId, ulidId: ulidId);
        return query is null ? [] : await query.ToListAsync(cancellationToken: ct);
    }

    public async Task<UserData?> GetUserDataSingleAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        IQueryable<UserData>? query = BuildQuery(context: context, userId: userId, type: type, intId: intId, ulidId: ulidId);
        return query is null ? null : await query.FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<int> DeleteUserDataAsync(
        List<UserData> userData,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        context.UserData.RemoveRange(entities: userData);
        return await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<int> RemoveForItemAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        // Guard: a null id must not fall through to `Column == null`, which would
        // match every row whose other id columns are null — a mass-delete. Require
        // the id for the requested type to be present before deleting anything.
        bool hasId = type switch
        {
            MediaTypes.MovieMediaType or MediaTypes.TvMediaType or MediaTypes.CollectionMediaType =>
                intId is not null,
            MediaTypes.SpecialMediaType => ulidId is not null,
            _ => false,
        };
        if (!hasId)
            return 0;

        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        IQueryable<UserData>? query = BuildQuery(context: context, userId: userId, type: type, intId: intId, ulidId: ulidId);
        return query is null ? 0 : await query.ExecuteDeleteAsync(cancellationToken: ct);
    }

    public async Task<int> HideFromContinueWatchingAsync(
        IEnumerable<UserData> userData,
        CancellationToken ct = default
    )
    {
        List<Ulid> ids = userData.Select(selector: data => data.Id).ToList();
        if (ids.Count == 0)
            return 0;

        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .UserData.Where(predicate: data => ids.Contains(data.Id))
            .ExecuteUpdateAsync(
                setPropertyCalls: setters => setters.SetProperty(propertyExpression: data => data.RemovedFromContinueWatching, valueExpression: true),
                cancellationToken: ct
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
            .Where(predicate: data => data.UserId.Equals(userId));

        return type switch
        {
            MediaTypes.MovieMediaType => query.Where(predicate: data => data.MovieId == intId),
            MediaTypes.TvMediaType => query.Where(predicate: data => data.TvId == intId),
            MediaTypes.SpecialMediaType => query.Where(predicate: data => data.SpecialId == ulidId),
            MediaTypes.CollectionMediaType => query.Where(predicate: data => data.CollectionId == intId),
            _ => null,
        };
    }
}
