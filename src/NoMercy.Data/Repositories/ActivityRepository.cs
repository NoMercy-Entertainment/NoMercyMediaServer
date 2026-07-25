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

namespace NoMercy.Data.Repositories;

public class ActivityRepository(MediaContext context) : IActivityRepository
{
    public Task<List<ActivityLog>> GetPagedAsync(
        ActivityCategory? category,
        Guid? userId,
        Ulid? deviceId,
        Ulid? mediaId,
        DateTime? from,
        DateTime? to,
        bool? success,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        IQueryable<ActivityLog> query = context
            .ActivityLogs.AsNoTracking()
            .Include(activityLog => activityLog.Device)
            .Include(activityLog => activityLog.User);

        if (category is { } cat)
            query = query.Where(activityLog => activityLog.Category == cat);
        if (userId is { } uid)
            query = query.Where(activityLog => activityLog.UserId == uid);
        if (deviceId is { } did)
            query = query.Where(activityLog => activityLog.DeviceId == did);
        if (mediaId is { } mid)
            query = query.Where(activityLog => activityLog.MediaId == mid);
        if (from is { } f)
            query = query.Where(activityLog => activityLog.CreatedAt >= f);
        if (to is { } t)
            query = query.Where(activityLog => activityLog.CreatedAt <= t);
        if (success is { } s)
            query = query.Where(activityLog => activityLog.Success == s);

        return query
            .OrderByDescending(activityLog => activityLog.CreatedAt)
            .ThenByDescending(activityLog => activityLog.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> DeleteAsync(
        ActivityCategory? category,
        DateTime? before,
        CancellationToken ct = default
    )
    {
        IQueryable<ActivityLog> query = context.ActivityLogs;

        if (category is { } cat)
            query = query.Where(activityLog => activityLog.Category == cat);
        if (before is { } cutoff)
            query = query.Where(activityLog => activityLog.CreatedAt < cutoff);

        return query.ExecuteDeleteAsync(ct);
    }
}
