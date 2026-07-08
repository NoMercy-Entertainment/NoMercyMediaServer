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

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveSessionIdleReaperTests
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

    /// <summary>
    /// Builds a LiveStreamingService pre-populated with a session whose
    /// LastAccess is offset by <paramref name="lastAccessOffset"/> from now.
    /// </summary>
    private static (LiveStreamingService service, string sessionId) BuildService(
        TimeSpan lastAccessOffset
    )
    {
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        service.Register(session, TimeSpan.FromSeconds(4));

        // Age the LastAccess timestamp.
        if (lastAccessOffset < TimeSpan.Zero)
        {
            // Simulate old access by accessing then rewinding via reflection.
            // Because LastAccess uses Interlocked ticks, we backdate it directly.
            BackdateLastAccess(service, session.SessionId, -lastAccessOffset);
        }

        return (service, session.SessionId);
    }

    private static void BackdateLastAccess(
        LiveStreamingService service,
        string sessionId,
        TimeSpan age
    )
    {
        if (!service.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return;

        // Touch with a timestamp in the past by calling the internal field via
        // reflection — keeps the test independent of clock skew.
        FieldInfo? field = typeof(LiveRuntimeSession).GetField(
            "_lastAccessTicks",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (field is null)
            return;

        long backdatedTicks = (DateTime.UtcNow - age).Ticks;
        field.SetValue(runtime, backdatedTicks);
    }

    private static LiveSessionIdleReaper BuildReaper(
        ILiveStreamingService streamingService,
        ISessionManager sessionManager,
        int idleTimeoutMinutes = 5
    ) =>
        new(
            streamingService,
            sessionManager,
            new() { IdleTimeoutMinutes = idleTimeoutMinutes },
            NullLogger<LiveSessionIdleReaper>.Instance
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Idle session gets evicted
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_IdleSession_IsDisposed()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            TimeSpan.FromMinutes(-6) // 6 min old — exceeds 5 min threshold
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(service, managerMock.Object);

        await reaper.SweepAsync();

        // After eviction the session is no longer registered.
        service.ActiveSessionIds.Should().NotContain(sessionId);
        managerMock.Verify(m => m.RemoveSession(sessionId), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Recently active session is left alone
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_ActiveSession_IsLeftAlone()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            TimeSpan.FromSeconds(-30) // 30 s old — well within 5 min threshold
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(service, managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().Contain(sessionId);
        managerMock.Verify(m => m.RemoveSession(It.IsAny<string>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Boundary: exactly at timeout — evict
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_SessionAtExactTimeout_IsEvicted()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            TimeSpan.FromMinutes(-5) // Exactly at the 5-min boundary
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(service, managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().NotContain(sessionId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multiple sessions: only idle ones evicted
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_MixedSessions_OnlyIdleEvicted()
    {
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession active = new(Ulid.NewUlid().ToString(), MakeQuality());
        LiveSession idle = new(Ulid.NewUlid().ToString(), MakeQuality());

        service.Register(active, TimeSpan.FromSeconds(4));
        service.Register(idle, TimeSpan.FromSeconds(4));

        // Age only the idle session
        BackdateLastAccess(service, idle.SessionId, TimeSpan.FromMinutes(10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(service, managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().Contain(active.SessionId);
        service.ActiveSessionIds.Should().NotContain(idle.SessionId);
    }
}
