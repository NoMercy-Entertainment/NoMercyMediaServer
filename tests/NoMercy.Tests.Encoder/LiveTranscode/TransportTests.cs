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
            Id: id,
            Label: id,
            Width: 1280,
            Height: 720,
            Codec: VideoCodecType.H264,
            BitrateKbps: 3000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static LiveSession MakeSession(string id = "sess-001") => new(sessionId: id, quality: MakeQuality());

    // ──────────────────────────────────────────────────────────────────────────
    // NoOp transport
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoOpTransport_SendToClientAsync_DoesNotThrow()
    {
        NoOpLiveSessionTransport transport = new();

        Func<Task> act = () =>
            transport.SendToClientAsync(sessionId: "s1", message: new HeartbeatMessage(), ct: CancellationToken.None);

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
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage),
            transport: transport
        );

        LiveSession session = MakeSession(id: "drain-test");
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Segment seg = new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/seg.ts", SizeBytes: 512);
        session.PushSegment(segment: seg);
        session.Complete();

        await WaitForConditionAsync(condition: () =>
        {
            return service.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession r)
                && r.SnapshotSegments().Count >= 1
                && transport.Sent.Count >= 1;
        });

        transport.Sent.Should().ContainSingle(predicate: m => m is SegmentReadyMessage);
        SegmentReadyMessage sent = (SegmentReadyMessage)transport.Sent[index: 0];
        sent.Index.Should().Be(expected: 0);
        sent.DurationSeconds.Should().BeApproximately(expectedValue: 6.0, precision: 0.001);
        sent.SizeBytes.Should().Be(expected: 512);
        sent.RelativeUrl.Should().Contain(expected: "drain-test");
    }

    [Fact]
    public async Task DrainLoop_WithNoOpTransport_SessionStillFunctional()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );

        LiveSession session = MakeSession(id: "noop-test");
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Segment seg = new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/seg.ts", SizeBytes: 100);
        session.PushSegment(segment: seg);
        session.Complete();

        await WaitForConditionAsync(condition: () =>
        {
            return service.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession r)
                && r.SnapshotSegments().Count >= 1;
        });

        service.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.TryGetSegment(index: 0, segment: out _).Should().BeTrue();
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
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage),
            transport: transport
        );

        LiveSession session = MakeSession(id: "reaper-test");
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        BackdateLastAccess(service: service, sessionId: session.SessionId, age: TimeSpan.FromMinutes(minutes: 10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = new(
            streamingService: service,
            sessionManager: managerMock.Object,
            limits: new() { IdleTimeoutMinutes = 5 },
            logger: NullLogger<LiveSessionIdleReaper>.Instance,
            transport: transport
        );

        await reaper.SweepAsync();

        transport.Sent.Should().ContainSingle(predicate: m => m is SessionEndedMessage);
        SessionEndedMessage ended = (SessionEndedMessage)transport.Sent[index: 0];
        ended.Reason.Should().Be(expected: SessionEndReason.ClientDisconnected);
    }

    [Fact]
    public async Task IdleReaper_WithNullTransport_EvictionStillWorks()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        LiveStreamingService service = new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );

        LiveSession session = MakeSession(id: "reaper-null");
        service.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        BackdateLastAccess(service: service, sessionId: session.SessionId, age: TimeSpan.FromMinutes(minutes: 10));

        Mock<ISessionManager> managerMock = new();
        LiveSessionIdleReaper reaper = new(
            streamingService: service,
            sessionManager: managerMock.Object,
            limits: new() { IdleTimeoutMinutes = 5 },
            logger: NullLogger<LiveSessionIdleReaper>.Instance
        );

        await reaper.SweepAsync();

        service.ActiveSessionIds.Should().NotContain(unexpected: session.SessionId);
        managerMock.Verify(expression: m => m.RemoveSession(session.SessionId), times: Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // QualityChanged — BufferAdaptiveService auto drop
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BufferAdaptiveService_PushesQualityChangedMessage_WithAutoAdaptiveReason()
    {
        CapturingTransport transport = new();
        LiveQuality newQuality = MakeQuality(id: "480p");

        transport.Sent.Should().BeEmpty();

        await transport.SendToClientAsync(
            sessionId: "adaptive-test",
            message: new QualityChangedMessage(
                NewQuality: newQuality,
                Reason: QualityChangeReason.AutoAdaptive
            ),
            ct: CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(predicate: m => m is QualityChangedMessage);
        QualityChangedMessage msg = (QualityChangedMessage)transport.Sent[index: 0];
        msg.Reason.Should().Be(expected: QualityChangeReason.AutoAdaptive);
        msg.NewQuality.Id.Should().Be(expected: "480p");
    }

    [Fact]
    public async Task BufferAdaptiveService_UserRequestedQuality_HasCorrectReason()
    {
        CapturingTransport transport = new();
        LiveQuality newQuality = MakeQuality(id: "1080p");

        await transport.SendToClientAsync(
            sessionId: "user-test",
            message: new QualityChangedMessage(
                NewQuality: newQuality,
                Reason: QualityChangeReason.UserRequested
            ),
            ct: CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(predicate: m => m is QualityChangedMessage);
        QualityChangedMessage msg = (QualityChangedMessage)transport.Sent[index: 0];
        msg.Reason.Should().Be(expected: QualityChangeReason.UserRequested);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TranscodeError — fired on abnormal runner exit
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TranscodeErrorMessage_HasExpectedShape()
    {
        CapturingTransport transport = new();

        TranscodeErrorMessage message = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: "FFmpeg exited with code 1",
            Recoverable: false
        );

        await transport.SendToClientAsync(sessionId: "error-test", message: message, ct: CancellationToken.None);

        transport.Sent.Should().ContainSingle(predicate: m => m is TranscodeErrorMessage);
        TranscodeErrorMessage received = (TranscodeErrorMessage)transport.Sent[index: 0];
        received.Kind.Should().Be(expected: EncodingErrorKind.ProcessCrashed);
        received.Recoverable.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SeekCompleted — round-trip shape
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeekCompletedMessage_HasExpectedShape()
    {
        CapturingTransport transport = new();

        SeekCompletedMessage message = new(NewPositionSeconds: 120.0, FirstSegmentIndex: 20);

        await transport.SendToClientAsync(sessionId: "seek-test", message: message, ct: CancellationToken.None);

        transport.Sent.Should().ContainSingle(predicate: m => m is SeekCompletedMessage);
        SeekCompletedMessage received = (SeekCompletedMessage)transport.Sent[index: 0];
        received.NewPositionSeconds.Should().Be(expected: 120.0);
        received.FirstSegmentIndex.Should().Be(expected: 20);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SessionEnded reasons
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: SessionEndReason.ClientDisconnected)]
    [InlineData(data: SessionEndReason.Completed)]
    [InlineData(data: SessionEndReason.Error)]
    [InlineData(data: SessionEndReason.ServerShutdown)]
    public async Task SessionEndedMessage_AllReasons_RoundTripThroughTransport(
        SessionEndReason reason
    )
    {
        CapturingTransport transport = new();

        await transport.SendToClientAsync(
            sessionId: "session-end",
            message: new SessionEndedMessage(Reason: reason),
            ct: CancellationToken.None
        );

        transport.Sent.Should().ContainSingle(predicate: m => m is SessionEndedMessage);
        SessionEndedMessage received = (SessionEndedMessage)transport.Sent[index: 0];
        received.Reason.Should().Be(expected: reason);
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
        if (!service.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return;

        FieldInfo? field = typeof(LiveRuntimeSession).GetField(
            name: "_lastAccessTicks",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        if (field is null)
            return;

        long backdatedTicks = (DateTime.UtcNow - age).Ticks;
        field.SetValue(obj: runtime, value: backdatedTicks);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(value: timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(millisecondsDelay: 10);
        }
        condition().Should().BeTrue(because: "the condition should have been met within the timeout");
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
        _sent.Add(item: message);
        return Task.CompletedTask;
    }
}
