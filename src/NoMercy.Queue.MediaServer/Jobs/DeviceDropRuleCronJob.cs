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
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercyQueue;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Queue.MediaServer.Jobs;

public class DeviceDropRuleCronJob : ICronJobExecutor
{
    private readonly MediaContext _context;
    private readonly ILogger<DeviceDropRuleCronJob> _logger;

    private static readonly TimeSpan GraceWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan EFuseWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan TtlWindow = TimeSpan.FromDays(7);

    public string CronExpression => new CronExpressionBuilder().Hourly();
    public string JobName => "Hourly Device Drop-Rule Job";

    public DeviceDropRuleCronJob(MediaContext context, ILogger<DeviceDropRuleCronJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Surface the inner exception under our own log channel so the
            // CronWorker's outer wrapper can't drop the diagnostic detail.
            _logger.LogError(
                ex,
                "DeviceDropRuleCronJob failed: {ErrorType} — {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
            throw;
        }
    }

    private async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        List<Device> candidates = await _context
            .Devices.Where(d => d.Fingerprint != null && d.OwnerUserId != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Device> toDrop = [];

        foreach (Device d in candidates)
        {
            DateTime? lastSeen = MaxOf(d.WsConnectedAt, d.MdnsSeenAt);
            if (lastSeen is null)
                continue;

            if (now - lastSeen.Value < GraceWindow)
                continue;

            if (now - lastSeen.Value >= TtlWindow)
            {
                toDrop.Add(d);
                continue;
            }

            if (now - lastSeen.Value < EFuseWindow)
                continue;

            if (string.IsNullOrEmpty(d.LanIp))
                continue;

            string lanIp = d.LanIp;
            string? fingerprint = d.Fingerprint;

            // EF/SQLite can't translate `now - column >= TimeSpan` — that
            // showed up as a runtime InvalidOperationException ("The LINQ
            // expression … could not be translated"). Pre-compute the cutoff
            // and compare DateTime <= DateTime, which translates cleanly.
            DateTime efuseCutoff = now - EFuseWindow;

            bool slotReclaimed = await _context
                .Devices.Where(o =>
                    o.LanIp == lanIp
                    && o.Fingerprint != fingerprint
                    && o.MdnsSeenAt != null
                    && o.MdnsSeenAt <= efuseCutoff
                )
                .AnyAsync(cancellationToken);

            if (slotReclaimed)
                toDrop.Add(d);
        }

        if (toDrop.Count == 0)
            return;

        List<Ulid> dropIds = toDrop.Select(d => d.Id).ToList();

        List<(Ulid DeviceId, Guid UserId, string Name, string Reason)> notices = toDrop
            .Where(d => d.OwnerUserId.HasValue)
            .Select(d =>
            {
                DateTime? lastSeen = MaxOf(d.WsConnectedAt, d.MdnsSeenAt);
                string reason =
                    lastSeen.HasValue && now - lastSeen.Value >= TtlWindow ? "ttl" : "efuse";
                string name = string.IsNullOrEmpty(d.CustomName) ? d.Name : d.CustomName!;
                return (d.Id, d.OwnerUserId!.Value, name, reason);
            })
            .ToList();

        List<Device> tracked = await _context
            .Devices.Where(d => dropIds.Contains(d.Id))
            .ToListAsync(cancellationToken);

        foreach (Device d in tracked)
        {
            d.OwnerUserId = null;
            d.IsActive = false;
        }

        foreach ((Ulid _, Guid userId, string name, string reason) in notices)
        {
            _context.DeviceDropNotices.Add(
                new DeviceDropNotice
                {
                    UserId = userId,
                    DeviceName = name,
                    Reason = reason,
                }
            );
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Dropped {Count} devices from registry", toDrop.Count);
    }

    private static DateTime? MaxOf(DateTime? a, DateTime? b) =>
        (a, b) switch
        {
            (null, null) => null,
            (null, _) => b,
            (_, null) => a,
            _ => a >= b ? a : b,
        };
}
