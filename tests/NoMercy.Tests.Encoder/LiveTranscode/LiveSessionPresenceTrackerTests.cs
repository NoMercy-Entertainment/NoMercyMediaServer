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

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// The presence tracker binds a live session's lifetime to the SignalR
/// connection watching it. These tests exercise the REAL disposal path: a real
/// LiveStreamingService holds a real registered session, and we assert the
/// session actually leaves ActiveSessionIds (or stays) — not that a mock was
/// called.
/// </summary>
public class LiveSessionPresenceTrackerTests
{
    private const string ConnA = "connection-A";
    private const string ConnB = "connection-B";

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

    private static (
        LiveStreamingService service,
        LiveSessionPresenceTracker tracker,
        string sessionId
    ) Build(int graceSeconds = 1)
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        LiveSessionLimits limits = new() { DisconnectGraceSeconds = graceSeconds };
        LiveSessionPresenceTracker tracker = new(
            streamingService: service,
            limits: limits,
            logger: NullLogger<LiveSessionPresenceTracker>.Instance
        );

        return (service, tracker, session.SessionId);
    }

    /// <summary>Polls until the session is gone or the timeout elapses.</summary>
    private static async Task<bool> WaitForRemovalAsync(
        LiveStreamingService service,
        string sessionId,
        TimeSpan timeout
    )
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (!service.ActiveSessionIds.Contains(value: sessionId))
                return true;
            await Task.Delay(millisecondsDelay: 50);
        }
        return service.ActiveSessionIds.Contains(value: sessionId) == false;
    }

    [Fact]
    public async Task ConnectionClosed_AfterGraceWindow_DisposesSession()
    {
        (LiveStreamingService service, LiveSessionPresenceTracker tracker, string sessionId) =
            Build(graceSeconds: 1);

        tracker.OnSubscribed(connectionId: ConnA, sessionId: sessionId);
        tracker.OnConnectionClosed(connectionId: ConnA);

        bool removed = await WaitForRemovalAsync(service: service, sessionId: sessionId, timeout: TimeSpan.FromSeconds(seconds: 3));

        removed.Should().BeTrue(because: "the watching connection closed and no reconnect arrived");
    }

    [Fact]
    public async Task Reconnect_WithinGraceWindow_KeepsSession()
    {
        (LiveStreamingService service, LiveSessionPresenceTracker tracker, string sessionId) =
            Build(graceSeconds: 1);

        tracker.OnSubscribed(connectionId: ConnA, sessionId: sessionId);
        tracker.OnConnectionClosed(connectionId: ConnA);

        // A network blip: the client reconnects on a fresh connection and
        // re-subscribes well inside the grace window.
        tracker.OnSubscribed(connectionId: ConnB, sessionId: sessionId);

        // Wait out the original grace window plus margin — the reconnect must
        // have cancelled the pending disposal.
        await Task.Delay(delay: TimeSpan.FromMilliseconds(milliseconds: 1600));

        service
            .ActiveSessionIds.Should()
            .Contain(expected: sessionId, because: "the reconnect re-subscribed before the grace window elapsed");
    }

    [Fact]
    public async Task Unsubscribe_ThenConnectionClosed_LeavesGracefulTeardownToCaller()
    {
        (LiveStreamingService service, LiveSessionPresenceTracker tracker, string sessionId) =
            Build(graceSeconds: 1);

        tracker.OnSubscribed(connectionId: ConnA, sessionId: sessionId);

        // Graceful player teardown: the client unsubscribes (its own DELETE will
        // dispose the session) and only then does the socket close.
        tracker.OnUnsubscribed(connectionId: ConnA, sessionId: sessionId);
        tracker.OnConnectionClosed(connectionId: ConnA);

        await Task.Delay(delay: TimeSpan.FromMilliseconds(milliseconds: 1600));

        service
            .ActiveSessionIds.Should()
            .Contain(
                expected: sessionId,
                because: "an explicit unsubscribe hands teardown to the caller, so the tracker must not dispose it"
            );
    }
}
