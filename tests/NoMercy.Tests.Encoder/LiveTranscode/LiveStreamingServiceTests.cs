namespace NoMercy.Tests.Encoder.LiveTranscode;

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;

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

    private static LiveSession MakeSession(string id = "sess-001") => new(id, MakeQuality());

    private static LiveStreamingService NewService() =>
        new(NullLogger<LiveStreamingService>.Instance);

    [Fact]
    public void Register_StoresRuntimeReachableViaTryGet()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();

        svc.Register(session, TimeSpan.FromSeconds(6));

        svc.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.Session.Should().BeSameAs(session);
        runtime.TargetSegmentDuration.Should().Be(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public void Register_TwiceForSameSessionId_Throws()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6));

        Action act = () => svc.Register(session, TimeSpan.FromSeconds(6));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Drainer_BuffersSegmentsIndexedByIndex()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6));

        Segment seg0 = new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/0.ts", 100);
        Segment seg1 = new(1, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6), "/tmp/1.ts", 100);
        session.PushSegment(seg0);
        session.PushSegment(seg1);
        session.Complete();

        await WaitForBufferAsync(svc, session.SessionId, expectedCount: 2);

        svc.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.TryGetSegment(0, out Segment found0).Should().BeTrue();
        found0.Should().Be(seg0);
        runtime.TryGetSegment(1, out Segment found1).Should().BeTrue();
        found1.Should().Be(seg1);
        runtime.TryGetSegment(99, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Drainer_CompletesWhenChannelCompletes()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6));

        session.Complete();

        await WaitForConditionAsync(() =>
        {
            return svc.TryGetRuntime(session.SessionId, out LiveRuntimeSession r) && r.IsComplete;
        });

        svc.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime).Should().BeTrue();
        runtime.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task SnapshotSegments_ReturnsSegmentsInIndexOrder()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6));

        session.PushSegment(
            new(2, TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(6), "/tmp/2.ts", 1)
        );
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(6), "/tmp/0.ts", 1));
        session.PushSegment(
            new(1, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6), "/tmp/1.ts", 1)
        );
        session.Complete();

        await WaitForBufferAsync(svc, session.SessionId, expectedCount: 3);

        svc.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime).Should().BeTrue();
        IReadOnlyList<Segment> snap = runtime.SnapshotSegments();
        snap.Select(s => s.Index).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task RemoveAsync_DisposesSessionAndRemovesFromMap()
    {
        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6));

        await svc.RemoveAsync(session.SessionId);

        svc.TryGetRuntime(session.SessionId, out _).Should().BeFalse();
        session.State.Should().Be(LiveSessionState.Ended);
    }

    [Fact]
    public async Task RemoveAsync_DeletesScratchDirectory_WhenProvided()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"live-scratch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "seg_00000.ts"), "data");

        try
        {
            LiveStreamingService svc = NewService();
            LiveSession session = MakeSession();
            svc.Register(session, TimeSpan.FromSeconds(6), tempDir);

            await svc.RemoveAsync(session.SessionId);

            Directory.Exists(tempDir).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveAsync_MissingScratchDirectory_SwallowsError()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"live-missing-{Guid.NewGuid():N}");

        LiveStreamingService svc = NewService();
        LiveSession session = MakeSession();
        svc.Register(session, TimeSpan.FromSeconds(6), nonExistent);

        Func<Task> act = () => svc.RemoveAsync(session.SessionId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ActiveSessionIds_ReflectsRegistrationAndRemoval()
    {
        LiveStreamingService svc = NewService();
        LiveSession s1 = MakeSession("a");
        LiveSession s2 = MakeSession("b");

        svc.Register(s1, TimeSpan.FromSeconds(6));
        svc.Register(s2, TimeSpan.FromSeconds(6));
        svc.ActiveSessionIds.Should().BeEquivalentTo(["a", "b"]);

        await svc.RemoveAsync("a");
        svc.ActiveSessionIds.Should().BeEquivalentTo(["b"]);
    }

    private static async Task WaitForBufferAsync(
        ILiveStreamingService svc,
        string sessionId,
        int expectedCount
    )
    {
        await WaitForConditionAsync(() =>
        {
            if (!svc.TryGetRuntime(sessionId, out LiveRuntimeSession r))
                return false;
            return r.SnapshotSegments().Count >= expectedCount;
        });
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
        condition()
            .Should()
            .BeTrue("the drainer should have processed the segments within timeout");
    }
}
