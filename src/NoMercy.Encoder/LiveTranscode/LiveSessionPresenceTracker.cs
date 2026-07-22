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

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.LiveTranscode.Protocol;

namespace NoMercy.Encoder.LiveTranscode;

/// <inheritdoc />
public sealed class LiveSessionPresenceTracker(
    ILiveStreamingService streamingService,
    LiveSessionLimits limits,
    ILogger<LiveSessionPresenceTracker> logger,
    ILiveSessionTransport? transport = null
) : ILiveSessionPresenceTracker
{
    // connectionId -> the session ids that connection is watching. The inner map
    // is used as a concurrent set (value byte is ignored).
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<string, byte>
    > _byConnection = new();

    // sessionId -> the CTS whose Task.Delay is counting down that session's grace
    // window. A reconnect cancels it; an elapsed window removes it and disposes.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingDisposal = new();

    private TimeSpan GraceWindow => TimeSpan.FromSeconds(seconds: limits.DisconnectGraceSeconds);

    public void OnSubscribed(string connectionId, string sessionId)
    {
        ConcurrentDictionary<string, byte> watched = _byConnection.GetOrAdd(
            key: connectionId,
            valueFactory: _ => new ConcurrentDictionary<string, byte>()
        );
        watched[key: sessionId] = 0;

        // The client is present again — cancel a grace-window disposal a previous
        // connection drop may have scheduled for this session.
        if (_pendingDisposal.TryRemove(key: sessionId, value: out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
            logger.LogDebug(
                message: "Live session {SessionId} kept alive: client re-subscribed within grace window",
                args: sessionId
            );
        }
    }

    public void OnUnsubscribed(string connectionId, string sessionId)
    {
        if (
            _byConnection.TryGetValue(key: connectionId, value: out ConcurrentDictionary<string, byte>? watched)
        )
            watched.TryRemove(key: sessionId, value: out _);
    }

    public void OnConnectionClosed(string connectionId)
    {
        if (!_byConnection.TryRemove(key: connectionId, value: out ConcurrentDictionary<string, byte>? watched))
            return;

        foreach (string sessionId in watched.Keys)
            ScheduleGraceDisposal(sessionId: sessionId);
    }

    private void ScheduleGraceDisposal(string sessionId)
    {
        CancellationTokenSource cts = new();

        // Another connection watching the same session already dropped and armed
        // the timer — leave the earlier one running rather than resetting it.
        if (!_pendingDisposal.TryAdd(key: sessionId, value: cts))
        {
            cts.Dispose();
            return;
        }

        _ = RunGraceDisposalAsync(sessionId: sessionId, ct: cts.Token);
    }

    private async Task RunGraceDisposalAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay: GraceWindow, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            // A reconnect re-subscribed inside the window — keep the session.
            return;
        }

        // Window elapsed without a reconnect. Drop the timer entry, then tear down.
        if (_pendingDisposal.TryRemove(key: sessionId, value: out CancellationTokenSource? cts))
            cts.Dispose();

        try
        {
            await PushSessionEndedAsync(sessionId: sessionId).ConfigureAwait(continueOnCapturedContext: false);
            await streamingService.RemoveAsync(sessionId: sessionId).ConfigureAwait(continueOnCapturedContext: false);
            logger.LogInformation(
                message: "Live session {SessionId} disposed after client tapped out (grace window elapsed)",
                args: sessionId
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to dispose live session {SessionId} after client disconnect",
                args: sessionId
            );
        }
    }

    private async Task PushSessionEndedAsync(string sessionId)
    {
        if (transport is null)
            return;

        try
        {
            await transport
                .SendToClientAsync(
                    sessionId: sessionId,
                    message: new SessionEndedMessage(Reason: SessionEndReason.ClientDisconnected),
                    ct: CancellationToken.None
                )
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                exception: ex,
                message: "Transport push failed for SessionEnded on session {SessionId}",
                args: sessionId
            );
        }
    }
}
