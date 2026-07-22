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

    private static LiveStreamingService NewStreamingService()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        return new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );
    }

    /// <summary>
    /// Builds a LiveStreamingService pre-populated with a session whose
    /// LastAccess is offset by <paramref name="lastAccessOffset"/> from now.
    /// </summary>
    private static (LiveStreamingService service, string sessionId) BuildService(
        TimeSpan lastAccessOffset
    )
    {
        LiveStreamingService service = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Age the LastAccess timestamp.
        if (lastAccessOffset < TimeSpan.Zero)
        {
            // Simulate old access by accessing then rewinding via reflection.
            // Because LastAccess uses Interlocked ticks, we backdate it directly.
            BackdateLastAccess(service: service, sessionId: session.SessionId, age: -lastAccessOffset);
        }

        return (service, session.SessionId);
    }

    private static void BackdateLastAccess(
        LiveStreamingService service,
        string sessionId,
        TimeSpan age
    )
    {
        if (!service.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        // Touch with a timestamp in the past by calling the internal field via
        // reflection — keeps the test independent of clock skew.
        FieldInfo? field = typeof(LiveRuntimeSession).GetField(
            name: "_lastAccessTicks",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (field is null)
            return;

        long backdatedTicks = (DateTime.UtcNow - age).Ticks;
        field.SetValue(obj: runtime, value: backdatedTicks);
    }

    private static LiveSessionIdleReaper BuildReaper(
        ILiveStreamingService streamingService,
        ISessionManager sessionManager,
        int idleTimeoutMinutes = 5
    ) =>
        new(
            streamingService: streamingService,
            sessionManager: sessionManager,
            limits: new() { IdleTimeoutMinutes = idleTimeoutMinutes },
            logger: NullLogger<LiveSessionIdleReaper>.Instance
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Idle session gets evicted
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_IdleSession_IsDisposed()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            lastAccessOffset: TimeSpan.FromMinutes(minutes: -6) // 6 min old — exceeds 5 min threshold
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(streamingService: service, sessionManager: managerMock.Object);

        await reaper.SweepAsync();

        // After eviction the session is no longer registered.
        service.ActiveSessionIds.Should().NotContain(unexpected: sessionId);
        managerMock.Verify(expression: m => m.RemoveSession(sessionId), times: Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Recently active session is left alone
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_ActiveSession_IsLeftAlone()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            lastAccessOffset: TimeSpan.FromSeconds(seconds: -30) // 30 s old — well within 5 min threshold
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(streamingService: service, sessionManager: managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().Contain(expected: sessionId);
        managerMock.Verify(expression: m => m.RemoveSession(It.IsAny<string>()), times: Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Boundary: exactly at timeout — evict
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_SessionAtExactTimeout_IsEvicted()
    {
        (LiveStreamingService service, string sessionId) = BuildService(
            lastAccessOffset: TimeSpan.FromMinutes(minutes: -5) // Exactly at the 5-min boundary
        );

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(streamingService: service, sessionManager: managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().NotContain(unexpected: sessionId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multiple sessions: only idle ones evicted
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Audio-rendition children are never idle-reaped
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SweepAsync_IdleAudioRenditionChild_IsNotEvicted()
    {
        LiveStreamingService service = NewStreamingService();

        LiveSession child = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        service.Register(session: child, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4), isAudioRenditionChild: true);

        // Idle far past the threshold — a non-selected language gets no hits, but
        // it must stay alive so a later switch to it works. The parent disposes it.
        BackdateLastAccess(service: service, sessionId: child.SessionId, age: TimeSpan.FromMinutes(minutes: 30));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(streamingService: service, sessionManager: managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().Contain(expected: child.SessionId);
        managerMock.Verify(expression: m => m.RemoveSession(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public async Task SweepAsync_MixedSessions_OnlyIdleEvicted()
    {
        LiveStreamingService service = NewStreamingService();

        LiveSession active = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        LiveSession idle = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());

        service.Register(session: active, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));
        service.Register(session: idle, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Age only the idle session
        BackdateLastAccess(service: service, sessionId: idle.SessionId, age: TimeSpan.FromMinutes(minutes: 10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = BuildReaper(streamingService: service, sessionManager: managerMock.Object);

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().Contain(expected: active.SessionId);
        service.ActiveSessionIds.Should().NotContain(unexpected: idle.SessionId);
    }
}
