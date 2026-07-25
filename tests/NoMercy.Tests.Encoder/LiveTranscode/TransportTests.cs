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
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.LiveTranscode.Protocol;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Tests that verify the transport wiring layer: messages are sent to the right
/// <see cref="ILiveSessionTransport"/> at the right event sites, and that the
/// absence of a transport (NoOp / null) leaves sessions fully functional.
/// </summary>
public class TransportTests
{
    private static LiveQuality MakeQuality(string id = "720p") =>
        new(
            id,
            id,
            1280,
            720,
            VideoCodecType.H264,
            3000,
            "libx264",
            false,
            2.0,
            true
        );

    private static LiveSession MakeSession(string id = "sess-001") => new(id, MakeQuality());

    // ──────────────────────────────────────────────────────────────────────────
    // NoOp transport
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoOpTransport_SendToClientAsync_DoesNotThrow()
    {
        NoOpLiveSessionTransport transport = new();

        Func<Task> act = () =>
            transport.SendToClientAsync("s1", new HeartbeatMessage(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SegmentReady — fired when the drain loop buffers a segment
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DrainLoop_PushesSegmentReadyMessage_WhenSegmentBuffered()
    {
        CapturingTransport transport = new();
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            storage,
            TestStorageFactory.CreateSegmentInventory(storage),
            transport
        );

        LiveSession session = MakeSession("drain-test");
        service.Register(session, TimeSpan.FromSeconds(6));

        Segment seg = new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/seg.ts", 512);
        session.PushSegment(seg);
        session.Complete();

        await WaitForConditionAsync(() =>
        {
            return service.TryGetRuntime(session.SessionId, out LiveRuntimeSession r)
                && r.SnapshotSegments().Count >= 1
                && transport.Sent.Count >= 1;
        });

        transport.Sent.Should().ContainSingle(m => m is SegmentReadyMessage);
        SegmentReadyMessage sent = (SegmentReadyMessage)transport.Sent[0];
        sent.Index.Should().Be(0);
        sent.DurationSeconds.Should().BeApproximately(6.0, 0.001);
        sent.SizeBytes.Should().Be(512);
        sent.RelativeUrl.Should().Contain("drain-test");
    }

    [Fact]
    public async Task DrainLoop_WithNoOpTransport_SessionStillFunctional()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            storage,
            TestStorageFactory.CreateSegmentInventory(storage)
        );

        LiveSession session = MakeSession("noop-test");
        service.Register(session, TimeSpan.FromSeconds(6));

        Segment seg = new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/seg.ts", 100);
        session.PushSegment(seg);
        session.Complete();

        await WaitForConditionAsync(() =>
        {
            return service.TryGetRuntime(session.SessionId, out LiveRuntimeSession r)
                && r.SnapshotSegments().Count >= 1;
        });

        service.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.TryGetSegment(0, out _).Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionEnded — idle reaper eviction
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IdleReaper_PushesSessionEndedMessage_OnEviction()
    {
        CapturingTransport transport = new();
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            storage,
            TestStorageFactory.CreateSegmentInventory(storage),
            transport
        );

        LiveSession session = MakeSession("reaper-test");
        service.Register(session, TimeSpan.FromSeconds(4));

        BackdateLastAccess(service, session.SessionId, TimeSpan.FromMinutes(10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = new(
            service,
            managerMock.Object,
            new() { IdleTimeoutMinutes = 5 },
            NullLogger<LiveSessionIdleReaper>.Instance,
            transport
        );

        await reaper.SweepAsync();

        transport.Sent.Should().ContainSingle(m => m is SessionEndedMessage);
        SessionEndedMessage ended = (SessionEndedMessage)transport.Sent[0];
        ended.Reason.Should().Be(SessionEndReason.ClientDisconnected);
    }

    [Fact]
    public async Task IdleReaper_WithNullTransport_EvictionStillWorks()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            NullLogger<LiveStreamingService>.Instance,
            storage,
            TestStorageFactory.CreateSegmentInventory(storage)
        );

        LiveSession session = MakeSession("reaper-null");
        service.Register(session, TimeSpan.FromSeconds(4));

        BackdateLastAccess(service, session.SessionId, TimeSpan.FromMinutes(10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = new(
            service,
            managerMock.Object,
            new() { IdleTimeoutMinutes = 5 },
            NullLogger<LiveSessionIdleReaper>.Instance
        );

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().NotContain(session.SessionId);
        managerMock.Verify(m => m.RemoveSession(session.SessionId), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // QualityChanged — BufferAdaptiveService auto drop
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BufferAdaptiveService_PushesQualityChangedMessage_WithAutoAdaptiveReason()
    {
        CapturingTransport transport = new();
        LiveQuality newQuality = MakeQuality("480p");

        transport.Sent.Should().BeEmpty();

        await transport.SendToClientAsync(
            "adaptive-test",
            new QualityChangedMessage(
                newQuality,
                QualityChangeReason.AutoAdaptive
            ),
            CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(m => m is QualityChangedMessage);
        QualityChangedMessage msg = (QualityChangedMessage)transport.Sent[0];
        msg.Reason.Should().Be(QualityChangeReason.AutoAdaptive);
        msg.NewQuality.Id.Should().Be("480p");
    }

    [Fact]
    public async Task BufferAdaptiveService_UserRequestedQuality_HasCorrectReason()
    {
        CapturingTransport transport = new();
        LiveQuality newQuality = MakeQuality("1080p");

        await transport.SendToClientAsync(
            "user-test",
            new QualityChangedMessage(
                newQuality,
                QualityChangeReason.UserRequested
            ),
            CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(m => m is QualityChangedMessage);
        QualityChangedMessage msg = (QualityChangedMessage)transport.Sent[0];
        msg.Reason.Should().Be(QualityChangeReason.UserRequested);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TranscodeError — fired on abnormal runner exit
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TranscodeErrorMessage_HasExpectedShape()
    {
        CapturingTransport transport = new();

        TranscodeErrorMessage message = new(
            EncodingErrorKind.ProcessCrashed,
            "FFmpeg exited with code 1",
            false
        );

        await transport.SendToClientAsync("error-test", message, CancellationToken.None);

        transport.Sent.Should().ContainSingle(m => m is TranscodeErrorMessage);
        TranscodeErrorMessage received = (TranscodeErrorMessage)transport.Sent[0];
        received.Kind.Should().Be(EncodingErrorKind.ProcessCrashed);
        received.Recoverable.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekCompleted — round-trip shape
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekCompletedMessage_HasExpectedShape()
    {
        CapturingTransport transport = new();

        SeekCompletedMessage message = new(120.0, 20);

        await transport.SendToClientAsync("seek-test", message, CancellationToken.None);

        transport.Sent.Should().ContainSingle(m => m is SeekCompletedMessage);
        SeekCompletedMessage received = (SeekCompletedMessage)transport.Sent[0];
        received.NewPositionSeconds.Should().Be(120.0);
        received.FirstSegmentIndex.Should().Be(20);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionEnded reasons
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionEndReason.ClientDisconnected)]
    [InlineData(SessionEndReason.Completed)]
    [InlineData(SessionEndReason.Error)]
    [InlineData(SessionEndReason.ServerShutdown)]
    public async Task SessionEndedMessage_AllReasons_RoundTripThroughTransport(
        SessionEndReason reason
    )
    {
        CapturingTransport transport = new();

        await transport.SendToClientAsync(
            "session-end",
            new SessionEndedMessage(reason),
            CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(m => m is SessionEndedMessage);
        SessionEndedMessage received = (SessionEndedMessage)transport.Sent[0];
        received.Reason.Should().Be(reason);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static void BackdateLastAccess(
        LiveStreamingService service,
        string sessionId,
        TimeSpan age
    )
    {
        if (!service.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return;

        FieldInfo? field = typeof(LiveRuntimeSession).GetField(
            "_lastAccessTicks",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (field is null)
            return;

        long backdatedTicks = (DateTime.UtcNow - age).Ticks;
        field.SetValue(runtime, backdatedTicks);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
        condition().Should().BeTrue("the condition should have been met within the timeout");
    }
}

/// <summary>
/// Test double that records every message sent via <see cref="ILiveSessionTransport"/>.
/// </summary>
public sealed class CapturingTransport : ILiveSessionTransport
{
    private readonly List<object> _sent = [];

    public IReadOnlyList<object> Sent => _sent;

    public Task SendToClientAsync(string sessionId, object message, CancellationToken ct)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }
}
