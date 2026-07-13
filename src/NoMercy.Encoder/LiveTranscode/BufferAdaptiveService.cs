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
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.LiveTranscode.Protocol;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Background service that evaluates buffer health for every active live session
/// every 5 seconds and acts on the result: suspends an over-buffered encoder,
/// resumes a suspended encoder when the buffer drains, and drops quality when
/// the buffer is critically low. Runs independently from the 30-second idle
/// reaper so adaptive reactions are fast enough to matter.
/// </summary>
public class BufferAdaptiveService(
    ILiveStreamingService streamingService,
    ILiveQualitySelector qualitySelector,
    BufferManager bufferManager,
    SpeedIndex speedIndex,
    IResourceBudget resourceBudget,
    ILogger<BufferAdaptiveService> logger,
    ILiveSessionTransport? transport = null
) : BackgroundService
{
    private static readonly TimeSpan EvalInterval = TimeSpan.FromSeconds(5);

    // A runner that just (re)started needs a few seconds to write its first
    // segment; until then its buffer is legitimately near-zero. Acting inside
    // this window would misread that as "drop quality" and cancel the fresh
    // runner every sweep, so a seek / quality-change / resume never completes.
    private static readonly TimeSpan TranscodeWarmup = TimeSpan.FromSeconds(10);

    // FFmpeg speed (× realtime) above which the encoder is comfortably keeping up.
    // A low client buffer while the encoder runs this fast just means the viewer
    // is near the production frontier (normal right after a seek), not that the
    // hardware can't sustain the resolution — so quality must not be dropped.
    private const double QualityKeepUpSpeed = 1.2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("BufferAdaptiveService started (eval interval = 5 s)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(EvalInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await EvaluateAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BufferAdaptiveService evaluation sweep failed; will retry");
            }
        }
    }

    internal async Task EvaluateAllAsync(CancellationToken ct)
    {
        foreach (string sessionId in streamingService.ActiveSessionIds)
        {
            if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
                continue;

            if (runtime.IsComplete)
                continue;

            ILiveSession session = runtime.Session;

            bool isSuspended = session.State == LiveSessionState.Buffered;
            BufferAction action = bufferManager.Evaluate(session.BufferAhead, isSuspended);

            // Suppress adaptive actions during the warm-up window after a
            // (re)start so the fresh runner is left alone to fill its buffer; the
            // client's own request pacing drives production in the meantime. State
            // is still reported below.
            if (DateTime.UtcNow - session.LastTranscodeStart < TranscodeWarmup)
                action = BufferAction.None;

            // Keep resolution up while the encoder is out-running realtime: a thin
            // buffer then reflects where the viewer is, not a struggling GPU.
            // Suspend/Resume still apply — only the quality drops are gated.
            if (
                action is BufferAction.DropQuality or BufferAction.EmergencyDropQuality
                && session.CurrentSpeed >= QualityKeepUpSpeed
            )
                action = BufferAction.None;

            switch (action)
            {
                case BufferAction.Suspend:
                    logger.LogDebug(
                        "BufferAdaptive: suspending session {SessionId} (buffer={Buf:F1}s)",
                        sessionId,
                        session.BufferAhead.TotalSeconds
                    );
                    session.Suspend();
                    break;

                case BufferAction.Resume:
                    logger.LogDebug(
                        "BufferAdaptive: resuming session {SessionId} (buffer={Buf:F1}s)",
                        sessionId,
                        session.BufferAhead.TotalSeconds
                    );
                    session.Resume();
                    break;

                case BufferAction.DropQuality:
                    await TryDropQualityAsync(
                            runtime,
                            session,
                            QualityChangeReason.AutoAdaptive,
                            ct
                        )
                        .ConfigureAwait(false);
                    break;

                case BufferAction.EmergencyDropQuality:
                    await TryDropQualityAsync(
                            runtime,
                            session,
                            QualityChangeReason.AutoAdaptive,
                            ct,
                            emergency: true
                        )
                        .ConfigureAwait(false);
                    break;

                case BufferAction.None:
                    break;
            }

            await PushTranscodeStateAsync(session, ct).ConfigureAwait(false);
        }
    }

    private async Task TryDropQualityAsync(
        LiveRuntimeSession runtime,
        ILiveSession session,
        QualityChangeReason reason,
        CancellationToken ct,
        bool emergency = false
    )
    {
        if (runtime.CachedMediaInfo is null || runtime.ClientCapabilities is null)
            return;

        LiveQuality[] available = qualitySelector.GetAvailableQualities(
            runtime.CachedMediaInfo,
            runtime.ClientCapabilities,
            speedIndex,
            resourceBudget
        );

        if (available.Length == 0)
            return;

        LiveQuality current = session.CurrentQuality;

        LiveQuality? target;
        if (emergency)
        {
            target = available[^1];
        }
        else
        {
            int currentIndex = Array.FindIndex(available, q => q.Id == current.Id);
            int nextIndex = currentIndex >= 0 ? currentIndex + 1 : available.Length - 1;
            target = nextIndex < available.Length ? available[nextIndex] : null;
        }

        if (target is null || target.Id == current.Id)
            return;

        logger.LogInformation(
            "BufferAdaptive: dropping quality {From} → {To} for session {SessionId} (reason={Reason}, buffer={Buf:F1}s)",
            current.Label,
            target.Label,
            session.SessionId,
            reason,
            session.BufferAhead.TotalSeconds
        );

        await session.ChangeQualityAsync(target.Id, target, ct).ConfigureAwait(false);
        await PushQualityChangedAsync(session, target, reason, ct).ConfigureAwait(false);
    }

    private async Task PushQualityChangedAsync(
        ILiveSession session,
        LiveQuality newQuality,
        QualityChangeReason reason,
        CancellationToken ct
    )
    {
        if (transport is null)
            return;

        QualityChangedMessage message = new(NewQuality: newQuality, Reason: reason);

        try
        {
            await transport.SendToClientAsync(session.SessionId, message, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Transport push failed for QualityChanged on session {SessionId}",
                session.SessionId
            );
        }
    }

    private async Task PushTranscodeStateAsync(ILiveSession session, CancellationToken ct)
    {
        if (transport is null)
            return;

        TranscodeStateMessage message = new(
            Speed: session.CurrentSpeed,
            BufferAheadSeconds: session.BufferAhead.TotalSeconds,
            State: session.State
        );

        try
        {
            await transport.SendToClientAsync(session.SessionId, message, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Transport push failed for TranscodeState on session {SessionId}",
                session.SessionId
            );
        }
    }
}
