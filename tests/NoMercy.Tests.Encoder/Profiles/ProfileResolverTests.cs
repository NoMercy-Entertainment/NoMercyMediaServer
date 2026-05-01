using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class ProfileResolverTests
{
    private readonly ProfileResolver _resolver = new();

    [Fact]
    public void Resolve_returns_child_unchanged_when_no_parent()
    {
        EncodingProfile child = NewProfile(name: "leaf");

        ProfileResolutionResult result = _resolver.Resolve(child, _ => null);

        result.CycleRule.Should().BeNull();
        result.Resolved.Should().NotBeNull();
        result.Resolved!.Id.Should().Be(child.Id);
        result.Resolved.Name.Should().Be("leaf");
    }

    [Fact]
    public void Resolve_inherits_scalar_field_from_parent_when_child_default()
    {
        EncodingProfile parent = NewProfile(name: "parent") with { Description = "from parent" };
        EncodingProfile child = NewProfile(name: "child") with { ParentId = parent.Id };

        ProfileResolutionResult result = _resolver.Resolve(
            child,
            id => id == parent.Id ? parent : null
        );

        result.Resolved!.Description.Should().Be("from parent");
    }

    [Fact]
    public void Resolve_child_scalar_wins_when_set()
    {
        EncodingProfile parent = NewProfile(name: "parent") with { Description = "from parent" };
        EncodingProfile child = NewProfile(name: "child") with
        {
            ParentId = parent.Id,
            Description = "from child",
        };

        ProfileResolutionResult result = _resolver.Resolve(
            child,
            id => id == parent.Id ? parent : null
        );

        result.Resolved!.Description.Should().Be("from child");
    }

    [Fact]
    public void Resolve_child_array_replaces_parent_array()
    {
        EncodingProfile parent = NewProfile(
            name: "parent",
            videoOutputs:
            [
                NewVideoOutput(width: 1920),
                NewVideoOutput(width: 1280),
                NewVideoOutput(width: 854),
            ]
        );
        EncodingProfile child = NewProfile(
            name: "child",
            videoOutputs: [NewVideoOutput(width: 1920)]
        ) with
        {
            ParentId = parent.Id,
        };

        ProfileResolutionResult result = _resolver.Resolve(
            child,
            id => id == parent.Id ? parent : null
        );

        result.Resolved!.VideoOutputs.Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_three_level_inheritance_walks_full_chain()
    {
        EncodingProfile grandparent = NewProfile(name: "gp") with { Description = "from gp" };
        EncodingProfile parent = NewProfile(name: "p") with { ParentId = grandparent.Id };
        EncodingProfile child = NewProfile(name: "c") with { ParentId = parent.Id };

        ProfileResolutionResult result = _resolver.Resolve(
            child,
            id =>
                id == parent.Id ? parent
                : id == grandparent.Id ? grandparent
                : null
        );

        result.Resolved!.Description.Should().Be("from gp");
    }

    [Fact]
    public void Resolve_id_name_isbuiltin_never_inherited()
    {
        EncodingProfile parent = NewProfile(name: "parent") with { IsBuiltin = true };
        EncodingProfile child = NewProfile(name: "child") with
        {
            ParentId = parent.Id,
            IsBuiltin = false,
        };

        ProfileResolutionResult result = _resolver.Resolve(
            child,
            id => id == parent.Id ? parent : null
        );

        result.Resolved!.Id.Should().Be(child.Id);
        result.Resolved.Name.Should().Be("child");
        result.Resolved.IsBuiltin.Should().BeFalse();
    }

    [Fact]
    public void Resolve_detects_two_node_cycle()
    {
        Ulid aId = Ulid.NewUlid();
        Ulid bId = Ulid.NewUlid();
        EncodingProfile a = NewProfile(name: "a", id: aId) with { ParentId = bId };
        EncodingProfile b = NewProfile(name: "b", id: bId) with { ParentId = aId };

        ProfileResolutionResult result = _resolver.Resolve(
            a,
            id =>
                id == aId ? a
                : id == bId ? b
                : null
        );

        result.Resolved.Should().BeNull();
        result.CycleRule.Should().NotBeNull();
        result.CycleRule!.Id.Should().Be(EncoderRuleId.ParentIdCycle);
        result.CycleRule.Severity.Should().Be(EncoderRuleSeverity.Error);
        result.CycleRule.Message.Should().Contain(aId.ToString());
        result.CycleRule.Message.Should().Contain(bId.ToString());
    }

    [Fact]
    public void Resolve_detects_three_node_cycle()
    {
        Ulid aId = Ulid.NewUlid();
        Ulid bId = Ulid.NewUlid();
        Ulid cId = Ulid.NewUlid();
        EncodingProfile a = NewProfile(name: "a", id: aId) with { ParentId = bId };
        EncodingProfile b = NewProfile(name: "b", id: bId) with { ParentId = cId };
        EncodingProfile c = NewProfile(name: "c", id: cId) with { ParentId = aId };

        ProfileResolutionResult result = _resolver.Resolve(
            a,
            id =>
                id == aId ? a
                : id == bId ? b
                : id == cId ? c
                : null
        );

        result.Resolved.Should().BeNull();
        result.CycleRule.Should().NotBeNull();
        result.CycleRule!.Id.Should().Be(EncoderRuleId.ParentIdCycle);
    }

    [Fact]
    public void Resolve_detects_self_cycle()
    {
        Ulid aId = Ulid.NewUlid();
        EncodingProfile a = NewProfile(name: "a", id: aId) with { ParentId = aId };

        ProfileResolutionResult result = _resolver.Resolve(a, id => id == aId ? a : null);

        result.Resolved.Should().BeNull();
        result.CycleRule.Should().NotBeNull();
        result.CycleRule!.Id.Should().Be(EncoderRuleId.ParentIdCycle);
    }

    [Fact]
    public void Resolve_handles_missing_parent_gracefully()
    {
        Ulid orphanedParent = Ulid.NewUlid();
        EncodingProfile child = NewProfile(name: "orphan") with { ParentId = orphanedParent };

        ProfileResolutionResult result = _resolver.Resolve(child, _ => null);

        result.CycleRule.Should().BeNull();
        result.Resolved.Should().NotBeNull();
        result.Resolved!.Name.Should().Be("orphan");
    }

    // ---------------------------------------------------------------------------

    private static EncodingProfile NewProfile(
        string name,
        Ulid? id = null,
        VideoOutput[]? videoOutputs = null
    ) =>
        new(
            Id: id ?? Ulid.NewUlid(),
            Name: name,
            Format: OutputFormat.Hls,
            VideoOutputs: videoOutputs ?? [NewVideoOutput()],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

    private static VideoOutput NewVideoOutput(int width = 1920, int height = 1080) =>
        new(
            Codec: VideoCodecType.H264,
            Width: width,
            Height: height,
            BitrateKbps: 5000,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
}
