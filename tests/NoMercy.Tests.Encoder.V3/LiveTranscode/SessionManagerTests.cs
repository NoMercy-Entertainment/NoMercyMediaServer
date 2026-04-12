namespace NoMercy.Tests.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.LiveTranscode;

public class SessionManagerTests
{
    private static LiveQuality MakeQuality() =>
        new(
            Id: "720p",
            Label: "720p",
            Width: 1280,
            Height: 720,
            Codec: VideoCodecType.H264,
            BitrateKbps: 4000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 1.5,
            CanRealtime: true
        );

    private static LiveSession MakeSession(string id) => new(id, MakeQuality());

    // ──────────────────────────────────────────────────────────────────────────
    // CanStartSession
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CanStartSession_WhenEmpty_ReturnsTrue()
    {
        SessionManager manager = new(new LiveSessionLimits { MaxConcurrentSessions = 4 });

        bool result = manager.CanStartSession();

        result.Should().BeTrue();
    }

    [Fact]
    public void CanStartSession_WhenAtMaxConcurrent_ReturnsFalse()
    {
        SessionManager manager = new(new LiveSessionLimits { MaxConcurrentSessions = 2 });
        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));

        bool result = manager.CanStartSession();

        result.Should().BeFalse();
    }

    [Fact]
    public void CanStartSession_WhenUnderMax_ReturnsTrue()
    {
        SessionManager manager = new(new LiveSessionLimits { MaxConcurrentSessions = 4 });
        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));

        bool result = manager.CanStartSession();

        result.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-user limit
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CanStartSession_WhenUserAtMax_ReturnsFalse()
    {
        SessionManager manager = new(
            new LiveSessionLimits { MaxConcurrentSessions = 10, MaxSessionsPerUser = 2 }
        );
        manager.RegisterSession(MakeSession("u1-s1"), userId: "user-1");
        manager.RegisterSession(MakeSession("u1-s2"), userId: "user-1");

        bool result = manager.CanStartSession(userId: "user-1");

        result.Should().BeFalse();
    }

    [Fact]
    public void CanStartSession_DifferentUser_NotAffectedByOtherUserLimit()
    {
        SessionManager manager = new(
            new LiveSessionLimits { MaxConcurrentSessions = 10, MaxSessionsPerUser = 2 }
        );
        manager.RegisterSession(MakeSession("u1-s1"), userId: "user-1");
        manager.RegisterSession(MakeSession("u1-s2"), userId: "user-1");

        bool result = manager.CanStartSession(userId: "user-2");

        result.Should().BeTrue();
    }

    [Fact]
    public void CanStartSession_NullUser_NotBoundByPerUserLimit()
    {
        SessionManager manager = new(
            new LiveSessionLimits { MaxConcurrentSessions = 10, MaxSessionsPerUser = 1 }
        );
        manager.RegisterSession(MakeSession("anon-1"));
        manager.RegisterSession(MakeSession("anon-2"));

        // Anonymous sessions are not tracked per-user
        bool result = manager.CanStartSession(userId: null);

        result.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RegisterSession / RemoveSession / ActiveSessionCount
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ActiveSessionCount_StartsAtZero()
    {
        SessionManager manager = new(new LiveSessionLimits());

        manager.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public void RegisterSession_IncreasesCount()
    {
        SessionManager manager = new(new LiveSessionLimits());

        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));

        manager.ActiveSessionCount.Should().Be(2);
    }

    [Fact]
    public void RemoveSession_DecreasesCount()
    {
        SessionManager manager = new(new LiveSessionLimits());
        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));

        manager.RemoveSession("s1");

        manager.ActiveSessionCount.Should().Be(1);
    }

    [Fact]
    public void RemoveSession_FreesSlot_AllowsNewSession()
    {
        SessionManager manager = new(new LiveSessionLimits { MaxConcurrentSessions = 2 });
        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));
        manager.CanStartSession().Should().BeFalse();

        manager.RemoveSession("s1");

        manager.CanStartSession().Should().BeTrue();
    }

    [Fact]
    public void RemoveSession_UnknownId_DoesNotThrow()
    {
        SessionManager manager = new(new LiveSessionLimits());

        Action act = () => manager.RemoveSession("nonexistent");

        act.Should().NotThrow();
    }

    [Fact]
    public void ActiveSessions_ReflectsRegisteredSessions()
    {
        SessionManager manager = new(new LiveSessionLimits());
        LiveSession session = MakeSession("s1");
        manager.RegisterSession(session);

        manager.ActiveSessions.Should().ContainSingle(s => s.SessionId == "s1");
    }

    [Fact]
    public void RemoveSession_ShouldRemoveFromActiveSessions()
    {
        SessionManager manager = new(new LiveSessionLimits());
        manager.RegisterSession(MakeSession("s1"));
        manager.RegisterSession(MakeSession("s2"));

        manager.RemoveSession("s1");

        manager.ActiveSessions.Should().ContainSingle(s => s.SessionId == "s2");
    }
}
