namespace NoMercy.Tests.Encoder.LiveTranscode;

using NoMercy.Encoder.LiveTranscode;

public class LivePlaylistBuilderTests
{
    private readonly LivePlaylistBuilder _builder = new();

    private static Segment MakeSegment(int index, double startSec, double durSec) =>
        new(
            index,
            TimeSpan.FromSeconds(startSec),
            TimeSpan.FromSeconds(durSec),
            $"/tmp/{index}.ts",
            100
        );

    [Fact]
    public void Build_EmptySegments_EmitsEventPlaylistWithNoEntries()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXTM3U");
        playlist.Should().Contain("#EXT-X-TARGETDURATION:6");
        playlist.Should().Contain("#EXT-X-MEDIA-SEQUENCE:0");
        playlist.Should().Contain("#EXT-X-PLAYLIST-TYPE:EVENT");
        playlist.Should().NotContain("#EXT-X-ENDLIST");
        playlist.Should().NotContain("#EXTINF");
    }

    [Fact]
    public void Build_WithSegments_EmitsExtinfAndUrlPerSegment()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(0, 0, 6), MakeSegment(1, 6, 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXTINF:6.000,");
        playlist.Should().Contain("/seg/0.ts");
        playlist.Should().Contain("/seg/1.ts");
    }

    [Fact]
    public void Build_Complete_EmitsVodTypeAndEndlist()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(0, 0, 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: true,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.Should().Contain("#EXT-X-ENDLIST");
        playlist.Should().NotContain("#EXT-X-PLAYLIST-TYPE:EVENT");
    }

    [Fact]
    public void Build_MediaSequence_FollowsFirstSegmentIndex()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(5, 30, 6), MakeSegment(6, 36, 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-MEDIA-SEQUENCE:5");
    }

    [Fact]
    public void Build_TargetDuration_UsesLongestSegmentWhenGreaterThanTarget()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(0, 0, 9.2)],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-TARGETDURATION:10");
    }

    [Fact]
    public void Build_EmptyUrlTemplate_Throws()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: ""
        );

        Action act = () => _builder.Build(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_IndexSubstitution_ReplacesAllIndexPlaceholders()
    {
        LivePlaylistRequest request = new(
            SessionId: "abc",
            Segments: [MakeSegment(2, 12, 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(6),
            IsComplete: false,
            SegmentUrlTemplate: "/live/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("/live/2.ts");
    }

    [Fact]
    public void Build_InvariantCulture_AlwaysUsesDotAsDecimalSeparator()
    {
        System.Globalization.CultureInfo prev = System
            .Threading
            .Thread
            .CurrentThread
            .CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new("nl-NL");

            LivePlaylistRequest request = new(
                SessionId: "s",
                Segments: [MakeSegment(0, 0, 6.5)],
                TargetSegmentDuration: TimeSpan.FromSeconds(6),
                IsComplete: false,
                SegmentUrlTemplate: "/seg/{index}.ts"
            );

            string playlist = _builder.Build(request);

            playlist.Should().Contain("#EXTINF:6.500,");
            playlist.Should().NotContain("6,500");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prev;
        }
    }
}
