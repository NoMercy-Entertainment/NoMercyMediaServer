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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Hubs;

[Authorize]
public class LiveTranscodeHub(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<MediaContext> contextFactory,
    ConnectedClients connectedClients,
    IActivityLogger activityLogger,
    ISessionManager sessionManager,
    ILiveStreamingService streamingService,
    ILiveSessionPresenceTracker presenceTracker,
    ILogger<LiveTranscodeHub> logger
) : ConnectionHub(httpContextAccessor: httpContextAccessor, contextFactory: contextFactory, connectedClients: connectedClients, activityLogger: activityLogger)
{
    public static string GroupName(string sessionId) => $"live-{sessionId}";

    /// <summary>
    /// Client calls this after receiving the session id from POST /sessions to
    /// start receiving server-push events for that session. Validates that the
    /// calling user owns the session before admitting them to the group.
    /// </summary>
    public async Task SubscribeToSession(string sessionId)
    {
        string? ownerId = sessionManager.GetOwnerUserId(sessionId: sessionId);

        if (ownerId is null)
        {
            logger.LogDebug(message: "SubscribeToSession: session {SessionId} not found", args: sessionId);
            return;
        }

        string callerId = Context.UserIdentifier ?? string.Empty;

        if (!string.Equals(a: ownerId, b: callerId, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                message: "SubscribeToSession: caller {CallerId} is not the owner of session {SessionId}", args: [callerId, sessionId]
            );
            return;
        }

        await Groups.AddToGroupAsync(connectionId: Context.ConnectionId, groupName: GroupName(sessionId: sessionId));
        presenceTracker.OnSubscribed(connectionId: Context.ConnectionId, sessionId: sessionId);

        logger.LogDebug(
            message: "Client {ConnectionId} subscribed to live session {SessionId}", args: [Context.ConnectionId, sessionId]
        );
    }

    /// <summary>
    /// Client leaves the session group. Counterpart to <see cref="SubscribeToSession"/>.
    /// </summary>
    public async Task UnsubscribeFromSession(string sessionId)
    {
        presenceTracker.OnUnsubscribed(connectionId: Context.ConnectionId, sessionId: sessionId);
        await Groups.RemoveFromGroupAsync(connectionId: Context.ConnectionId, groupName: GroupName(sessionId: sessionId));
    }

    /// <summary>
    /// A watching connection dropped (tab close, navigation, network loss). Hand
    /// off to the presence tracker, which disposes the connection's sessions once
    /// a short grace window elapses without a reconnect re-subscribing.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        presenceTracker.OnConnectionClosed(connectionId: Context.ConnectionId);
        await base.OnDisconnectedAsync(exception: exception);
    }

    /// <summary>
    /// Client heartbeat — updates the session last-activity timestamp so the
    /// idle reaper does not evict an active session.
    /// </summary>
    public void Heartbeat(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        string? ownerId = sessionManager.GetOwnerUserId(sessionId: sessionId);
        string callerId = Context.UserIdentifier ?? string.Empty;

        if (!string.Equals(a: ownerId, b: callerId, comparisonType: StringComparison.OrdinalIgnoreCase))
            return;

        runtime.TouchLastAccess();
    }

    /// <summary>
    /// The watching client reports its true playback position, so buffer-ahead
    /// is measured from where the user is actually watching rather than from
    /// the prefetch frontier (how far the player has fetched segments ahead).
    /// Older clients that never call this keep getting the pre-fix,
    /// segment-request-derived estimate — see
    /// <see cref="NoMercy.Api.Services.LiveTranscodeService.GetSegmentAsync"/>.
    /// </summary>
    public void ReportPlayhead(string sessionId, double currentTimeSeconds)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId: sessionId))
            return;

        runtime.Session.ReportPlaybackPosition(
            position: TimeSpan.FromSeconds(value: Math.Max(val1: 0, val2: currentTimeSeconds)),
            authoritative: true
        );
        runtime.TouchLastAccess();
    }

    /// <summary>
    /// The watching client reports its download-buffer depth (seconds of media
    /// it holds downloaded but not yet played) and its measured/estimated
    /// downlink in kbps. This is a NETWORK signal, distinct from the
    /// encoder-capacity <see cref="ILiveSession.BufferAhead"/> the server
    /// already tracks — it drives the buffer-adaptive sweep's network axis
    /// (emergency drop / bandwidth-fit drop / hysteresis-gated raise). Older
    /// clients that never call this keep getting today's encoder-lead-only
    /// adaptive behavior — see
    /// <see cref="NoMercy.Encoder.LiveTranscode.BufferAdaptiveService"/>.
    /// </summary>
    public void ReportBufferHealth(
        string sessionId,
        double bufferedSeconds,
        double observedBandwidthKbps
    )
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId: sessionId))
            return;

        runtime.Session.ReportClientBufferHealth(
            bufferedAhead: TimeSpan.FromSeconds(value: Math.Max(val1: 0, val2: bufferedSeconds)),
            observedBandwidthKbps: (int)Math.Max(val1: 0, val2: observedBandwidthKbps)
        );
        runtime.TouchLastAccess();
    }

    /// <summary>
    /// Client requests the encoder pause (fill buffer to max, stop producing
    /// new segments). Maps to <see cref="ILiveSession.Suspend"/>.
    /// </summary>
    public void RequestPause(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId: sessionId))
            return;

        runtime.Session.Suspend();
    }

    /// <summary>
    /// Client requests the encoder resume after a pause.
    /// Maps to <see cref="ILiveSession.Resume"/>.
    /// </summary>
    public void RequestResume(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        if (!CallerOwnsSession(sessionId: sessionId))
            return;

        runtime.Session.Resume();
    }

    private bool CallerOwnsSession(string sessionId)
    {
        string? ownerId = sessionManager.GetOwnerUserId(sessionId: sessionId);
        string callerId = Context.UserIdentifier ?? string.Empty;
        return string.Equals(a: ownerId, b: callerId, comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
