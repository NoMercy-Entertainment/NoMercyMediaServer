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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Branch-coverage gaps for <see cref="LiveSessionIdleReaper"/> beyond the four
/// happy-path / boundary tests in <see cref="LiveSessionIdleReaperTests"/>:
///
/// • Complete session — even if it has been idle for hours, IsComplete=true
///   means the encode finished naturally; the reaper must not double-evict.
/// • RemoveAsync throws — error logged, loop continues to next session.
///   A flaky storage delete must NOT bring down the reaper.
/// • No active sessions — sweep runs but no-ops cleanly.
/// • Session manager throws — same swallow-and-continue contract.
/// </summary>
public class LiveSessionIdleReaperBranchTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static LiveSessionIdleReaper BuildReaper(
        ILiveStreamingService streamingService,
        ISessionManager sessionManager,
        int idleTimeoutMinutes = 5
    ) =>
        new(
            streamingService,
            sessionManager,
            new LiveSessionLimits { IdleTimeoutMinutes = idleTimeoutMinutes },
            NullLogger<LiveSessionIdleReaper>.Instance
        );

    // ── No active sessions ──────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_no_sessions_no_op()
    {
        Mock<ILiveStreamingService> service = new();
        service.Setup(s => s.ActiveSessionIds).Returns(Array.Empty<string>());

        Mock<ISessionManager> sessionMgr = new();
        LiveSessionIdleReaper reaper = BuildReaper(service.Object, sessionMgr.Object);

        Func<Task> act = () => reaper.SweepAsync();
        await act.Should().NotThrowAsync();

        service.Verify(s => s.RemoveAsync(It.IsAny<string>()), Times.Never);
        sessionMgr.Verify(m => m.RemoveSession(It.IsAny<string>()), Times.Never);
    }

    // ── Completed session NOT evicted even if idle ──────────────────────────

    [Fact]
    public async Task SweepAsync_complete_session_is_not_evicted()
    {
        LiveStreamingService realService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );
        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        realService.Register(session, TimeSpan.FromSeconds(4));

        // Age the session AND mark complete via reflection on the runtime.
        if (realService.TryGetRuntime(session.SessionId, out LiveRuntimeSession? runtime))
        {
            // Backdate by 10 minutes via reflection.
            System.Reflection.FieldInfo? field = typeof(LiveRuntimeSession).GetField(
                "_lastAccessTicks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            field?.SetValue(runtime, DateTime.UtcNow.AddMinutes(-10).Ticks);

            // Mark complete via reflection on the internal method.
            System.Reflection.MethodInfo? markComplete = typeof(LiveRuntimeSession).GetMethod(
                "MarkComplete",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            markComplete?.Invoke(runtime, []);
        }

        Mock<ISessionManager> sessionMgr = new();
        LiveSessionIdleReaper reaper = BuildReaper(realService, sessionMgr.Object);

        await reaper.SweepAsync();

        // Complete sessions are excluded from idle eviction — the runtime is
        // still registered (it'll be removed via the normal completion path).
        realService.ActiveSessionIds.Should().Contain(session.SessionId);
        sessionMgr.Verify(m => m.RemoveSession(It.IsAny<string>()), Times.Never);
    }

    // ── Eviction failure propagation ────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_swallows_eviction_failure_and_continues_with_next()
    {
        // Use real LiveStreamingService + a stub session manager that throws
        // on RemoveSession for the first idle session. The reaper must still
        // process the second idle session.
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession first = new(Ulid.NewUlid().ToString(), MakeQuality());
        LiveSession second = new(Ulid.NewUlid().ToString(), MakeQuality());

        streamingService.Register(first, TimeSpan.FromSeconds(4));
        streamingService.Register(second, TimeSpan.FromSeconds(4));

        // Backdate both so they're idle.
        foreach (string id in new[] { first.SessionId, second.SessionId })
        {
            if (streamingService.TryGetRuntime(id, out LiveRuntimeSession? runtime))
            {
                System.Reflection.FieldInfo? field = typeof(LiveRuntimeSession).GetField(
                    "_lastAccessTicks",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                field?.SetValue(runtime, DateTime.UtcNow.AddMinutes(-10).Ticks);
            }
        }

        Mock<ISessionManager> sessionMgr = new();
        // Throw on the FIRST session's removal — loop must continue.
        sessionMgr
            .Setup(m => m.RemoveSession(first.SessionId))
            .Throws(new InvalidOperationException("session manager exploded"));

        LiveSessionIdleReaper reaper = BuildReaper(streamingService, sessionMgr.Object);

        Func<Task> act = () => reaper.SweepAsync();
        await act.Should().NotThrowAsync();

        // Second session's removal was attempted (proves loop didn't bail).
        sessionMgr.Verify(m => m.RemoveSession(second.SessionId), Times.Once);
    }
}
