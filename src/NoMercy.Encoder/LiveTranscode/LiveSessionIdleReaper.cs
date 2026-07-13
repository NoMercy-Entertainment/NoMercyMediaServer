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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.LiveTranscode.Protocol;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Background service that sweeps active live sessions every 30 seconds and
/// disposes any session that has had no playlist or segment hit for longer than
/// <see cref="LiveSessionLimits.IdleTimeoutMinutes"/>. Temp directories are
/// cleaned by <see cref="ILiveStreamingService.RemoveAsync"/>.
/// </summary>
public class LiveSessionIdleReaper(
    ILiveStreamingService streamingService,
    ISessionManager sessionManager,
    LiveSessionLimits limits,
    ILogger<LiveSessionIdleReaper> logger,
    ILiveSessionTransport? transport = null
) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug(
            "LiveSessionIdleReaper started (idle timeout = {Min} min)",
            limits.IdleTimeoutMinutes
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await SweepAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Don't let a sweep failure crash the host. Idle eviction is
                // best-effort — log and try again next interval.
                logger.LogWarning(ex, "LiveSessionIdleReaper sweep failed; will retry");
            }
        }
    }

    internal async Task SweepAsync()
    {
        DateTime cutoff = DateTime.UtcNow.AddMinutes(-limits.IdleTimeoutMinutes);

        List<string> toEvict = [];
        foreach (string sessionId in streamingService.ActiveSessionIds)
        {
            if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
                continue;

            // A per-language audio child gets no segment hits while another
            // language is selected, so idle-reaping it would break a later switch.
            // Its lifetime is bound to the parent, which cascade-disposes it.
            if (runtime.IsAudioRenditionChild)
                continue;

            if (runtime.IsComplete)
                continue;

            if (runtime.LastAccess < cutoff)
            {
                toEvict.Add(sessionId);
            }
        }

        foreach (string sessionId in toEvict)
        {
            logger.LogInformation(
                "LiveSessionIdleReaper evicting idle session {SessionId}",
                sessionId
            );

            try
            {
                await PushSessionEndedAsync(sessionId, SessionEndReason.ClientDisconnected)
                    .ConfigureAwait(false);
                await streamingService.RemoveAsync(sessionId).ConfigureAwait(false);
                sessionManager.RemoveSession(sessionId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "LiveSessionIdleReaper failed to evict session {SessionId}",
                    sessionId
                );
            }
        }
    }

    private async Task PushSessionEndedAsync(string sessionId, SessionEndReason reason)
    {
        if (transport is null)
            return;

        SessionEndedMessage message = new(Reason: reason);

        try
        {
            await transport
                .SendToClientAsync(sessionId, message, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Transport push failed for SessionEnded on session {SessionId}",
                sessionId
            );
        }
    }
}
