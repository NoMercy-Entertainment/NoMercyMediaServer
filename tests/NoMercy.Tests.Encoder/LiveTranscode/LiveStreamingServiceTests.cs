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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveStreamingServiceTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "720p",
            Label: "720p",
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

    private static LiveStreamingService NewService()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        return new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );
    }

    [Fact]
    public void Register_StoresRuntimeReachableViaTryGet()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();

        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        svc.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.Session.Should().BeSameAs(expected: session);
        runtime.TargetSegmentDuration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 6));
    }

    [Fact]
    public void Register_TwiceForSameSessionId_Throws()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Action act = () => svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Drainer_BuffersSegmentsIndexedByIndex()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        Segment seg0 = new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/0.ts", SizeBytes: 100);
        Segment seg1 = new(Index: 1, StartTime: TimeSpan.FromSeconds(seconds: 6), Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/1.ts", SizeBytes: 100);
        session.PushSegment(segment: seg0);
        session.PushSegment(segment: seg1);
        session.Complete();

        await WaitForBufferAsync(svc: svc, sessionId: session.SessionId, expectedCount: 2);

        svc.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.TryGetSegment(index: 0, segment: out Segment found0).Should().BeTrue();
        found0.Should().Be(expected: seg0);
        runtime.TryGetSegment(index: 1, segment: out Segment found1).Should().BeTrue();
        found1.Should().Be(expected: seg1);
        runtime.TryGetSegment(index: 99, segment: out _).Should().BeFalse();
    }

    [Fact]
    public async Task Drainer_CompletesWhenChannelCompletes()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        session.Complete();

        await WaitForConditionAsync(condition: () =>
        {
            return svc.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession r) && r.IsComplete;
        });

        svc.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task SnapshotSegments_ReturnsSegmentsInIndexOrder()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        session.PushSegment(
            segment: new(Index: 2, StartTime: TimeSpan.FromSeconds(seconds: 12), Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/2.ts", SizeBytes: 1)
        );
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/0.ts", SizeBytes: 1));
        session.PushSegment(
            segment: new(Index: 1, StartTime: TimeSpan.FromSeconds(seconds: 6), Duration: TimeSpan.FromSeconds(seconds: 6), FilePath: "/tmp/1.ts", SizeBytes: 1)
        );
        session.Complete();

        await WaitForBufferAsync(svc: svc, sessionId: session.SessionId, expectedCount: 3);

        svc.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        IReadOnlyList<Segment> snap = runtime.SnapshotSegments();
        snap.Select(selector: s => s.Index).Should().Equal(elements: [0, 1, 2]);
    }

    [Fact]
    public async Task RemoveAsync_DisposesSessionAndRemovesFromMap()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        await svc.RemoveAsync(sessionId: session.SessionId);

        svc.TryGetRuntime(sessionId: session.SessionId, runtime: out _).Should().BeFalse();
        session.State.Should().Be(expected: LiveSessionState.Ended);
    }

    [Fact]
    public async Task RemoveAsync_DeletesScratchDirectory_WhenProvided()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"live-scratch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: tempDir, path2: "seg_00000.ts"), contents: "data");

        try
        {
            LiveStreamingService svc = NewService();
            LiveSession session = MakeSession();
            svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6), scratchDirectory: tempDir);

            await svc.RemoveAsync(sessionId: session.SessionId);

            Directory.Exists(path: tempDir).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(path: tempDir))
                Directory.Delete(path: tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveAsync_MissingScratchDirectory_SwallowsError()
    {
        string nonExistent = Path.Combine(path1: Path.GetTempPath(), path2: $"live-missing-{Guid.NewGuid():N}");

        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6), scratchDirectory: nonExistent);

        Func<Task> act = () => svc.RemoveAsync(sessionId: session.SessionId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetActiveSessions_NoSessions_ReturnsEmpty()
    {
        LiveStreamingService svc = NewService();

        IReadOnlyList<LiveSessionSnapshot> result = svc.GetActiveSessions();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveSessions_WithRegisteredSession_ReturnsIt()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession(id: "snap-001");
        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        IReadOnlyList<LiveSessionSnapshot> result = svc.GetActiveSessions();

        result.Should().HaveCount(expected: 1);
        LiveSessionSnapshot snap = result[index: 0];
        snap.SessionId.Should().Be(expected: "snap-001");
        snap.QualityId.Should().Be(expected: "720p");
        snap.Width.Should().Be(expected: 1280);
        snap.Height.Should().Be(expected: 720);
        snap.BitrateKbps.Should().Be(expected: 3000);
        snap.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveSessions_AfterRemoval_ExcludesRemoved()
    {
        LiveStreamingService svc = NewService();
        LiveSession s1 = MakeSession(id: "remove-a");
        LiveSession s2 = MakeSession(id: "remove-b");

        svc.Register(session: s1, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        svc.Register(session: s2, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));

        await svc.RemoveAsync(sessionId: "remove-a");

        IReadOnlyList<LiveSessionSnapshot> result = svc.GetActiveSessions();

        result.Should().HaveCount(expected: 1);
        result[index: 0].SessionId.Should().Be(expected: "remove-b");
    }

    [Fact]
    public async Task ActiveSessionIds_ReflectsRegistrationAndRemoval()
    {
        LiveStreamingService svc = NewService();
        LiveSession s1 = MakeSession(id: "a");
        LiveSession s2 = MakeSession(id: "b");

        svc.Register(session: s1, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        svc.Register(session: s2, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        svc.ActiveSessionIds.Should().BeEquivalentTo(expectation: ["a", "b"]);

        await svc.RemoveAsync(sessionId: "a");
        svc.ActiveSessionIds.Should().BeEquivalentTo(expectation: "b");
    }

    [Fact]
    public void Register_AsAudioRenditionChild_FlagsRuntime()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession(id: "child-1");

        svc.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6), isAudioRenditionChild: true);

        svc.TryGetRuntime(sessionId: "child-1", runtime: out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.IsAudioRenditionChild.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_CascadesToChildAudioSessions()
    {
        LiveStreamingService svc = NewService();
        LiveSession parent = MakeSession(id: "parent");
        LiveSession childA = MakeSession(id: "audio-eng");
        LiveSession childB = MakeSession(id: "audio-jpn");

        svc.Register(session: parent, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6));
        svc.Register(session: childA, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6), isAudioRenditionChild: true);
        svc.Register(session: childB, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 6), isAudioRenditionChild: true);
        svc.StampChildAudioSessions(sessionId: "parent", childSessionIds: ["audio-eng", "audio-jpn"]);

        await svc.RemoveAsync(sessionId: "parent");

        // Removing the video session disposes its per-language audio children too,
        // so a switch can never target an audio track whose video is already gone.
        svc.TryGetRuntime(sessionId: "parent", runtime: out _).Should().BeFalse();
        svc.TryGetRuntime(sessionId: "audio-eng", runtime: out _).Should().BeFalse();
        svc.TryGetRuntime(sessionId: "audio-jpn", runtime: out _).Should().BeFalse();
        childA.State.Should().Be(expected: LiveSessionState.Ended);
        childB.State.Should().Be(expected: LiveSessionState.Ended);
    }

    private static async Task WaitForBufferAsync(
        ILiveStreamingService svc,
        string sessionId,
        int expectedCount
    )
    {
        await WaitForConditionAsync(condition: () =>
        {
            if (!svc.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession r))
                return false;
            return r.SnapshotSegments().Count >= expectedCount;
        });
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(value: timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(millisecondsDelay: 10);
        }
        condition()
            .Should()
            .BeTrue(because: "the drainer should have processed the segments within timeout");
    }
}
