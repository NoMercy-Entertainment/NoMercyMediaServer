namespace NoMercy.Tests.Encoder.Profiles;

using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class PresetResolverTests
{
    [Fact]
    public void Resolve_StandalonePreset_ReturnsProfileFromJson()
    {
        EncodingProfile profile = BuildBaseProfile();
        string json = JsonConvert.SerializeObject(profile);
        PresetResolveRequest request = new("Anime 1080p", json);

        EncodingProfile resolved = new PresetResolver().Resolve(request, new DictionaryLookup([]));

        resolved.Name.Should().Be(profile.Name);
        resolved.VideoOutputs.Should().HaveCount(1);
        resolved.VideoOutputs[0].Crf.Should().Be(23);
    }

    [Fact]
    public void Resolve_ChildOverridesParentField()
    {
        EncodingProfile parentProfile = BuildBaseProfile() with
        {
            Name = "Archive",
            VideoOutputs = [BuildVideoOutput(crf: 18)],
        };
        EncodingProfile childProfile = BuildBaseProfile() with
        {
            Name = "Archive Extra",
            VideoOutputs = [BuildVideoOutput(crf: 15)],
        };

        DictionaryLookup lookup = new([new("Archive", JsonConvert.SerializeObject(parentProfile))]);

        PresetResolveRequest request = new(
            Name: "Archive Extra",
            ProfileJson: JsonConvert.SerializeObject(childProfile),
            ParentName: "Archive"
        );

        EncodingProfile resolved = new PresetResolver().Resolve(request, lookup);

        resolved.VideoOutputs[0].Crf.Should().Be(15);
        resolved.Name.Should().Be("Archive Extra");
    }

    [Fact]
    public void Resolve_ChildOmitsField_InheritsFromParent()
    {
        // Parent has AutoLadder=true. Child's JSON doesn't mention AutoLadder
        // → resolved profile must retain the parent value (PopulateObject
        // only overwrites fields present in the incoming JSON).
        EncodingProfile parentProfile = BuildBaseProfile() with
        {
            AutoLadder = true,
        };

        // Child JSON without AutoLadder field — craft manually so the missing
        // key actually stays absent.
        string childJson = """
            {
                "Name": "Override",
                "VideoOutputs": []
            }
            """;

        DictionaryLookup lookup = new([new("Base", JsonConvert.SerializeObject(parentProfile))]);

        PresetResolveRequest request = new(
            Name: "Override",
            ProfileJson: childJson,
            ParentName: "Base"
        );

        EncodingProfile resolved = new PresetResolver().Resolve(request, lookup);

        resolved.AutoLadder.Should().BeTrue();
        resolved.Name.Should().Be("Override");
    }

    [Fact]
    public void Resolve_ThreeLevelChain_ComposesInOrder()
    {
        // Root carries nothing interesting. Mid flips AutoLadder on. Leaf
        // flips AutoDetectCrop on *without* mentioning AutoLadder — the
        // expected result is both flags true because the leaf only overlays
        // the keys it explicitly serializes.
        EncodingProfile root = BuildBaseProfile() with
        {
            Name = "Root",
        };
        EncodingProfile mid = BuildBaseProfile() with { Name = "Mid", AutoLadder = true };

        DictionaryLookup lookup = new([
            new("Root", JsonConvert.SerializeObject(root)),
            new("Mid", JsonConvert.SerializeObject(mid), "Root"),
        ]);

        // Sparse leaf JSON — only mentions AutoDetectCrop to test inheritance.
        string leafJson = """{ "Name": "Leaf", "AutoDetectCrop": true }""";

        PresetResolveRequest request = new(Name: "Leaf", ProfileJson: leafJson, ParentName: "Mid");

        EncodingProfile resolved = new PresetResolver().Resolve(request, lookup);

        resolved.AutoLadder.Should().BeTrue();
        resolved.AutoDetectCrop.Should().BeTrue();
        resolved.Name.Should().Be("Leaf");
    }

    [Fact]
    public void Resolve_CycleInParentChain_Throws()
    {
        // A → B → A cycle
        EncodingProfile a = BuildBaseProfile() with
        {
            Name = "A",
        };
        EncodingProfile b = BuildBaseProfile() with { Name = "B" };

        DictionaryLookup lookup = new([
            new("A", JsonConvert.SerializeObject(a), "B"),
            new("B", JsonConvert.SerializeObject(b), "A"),
        ]);

        PresetResolveRequest request = new(
            Name: "C",
            ProfileJson: JsonConvert.SerializeObject(a),
            ParentName: "A"
        );

        Action act = () => new PresetResolver().Resolve(request, lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Resolve_MissingParent_Throws()
    {
        EncodingProfile profile = BuildBaseProfile();
        PresetResolveRequest request = new(
            Name: "Orphan",
            ProfileJson: JsonConvert.SerializeObject(profile),
            ParentName: "DoesNotExist"
        );

        Action act = () => new PresetResolver().Resolve(request, new DictionaryLookup([]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed record Entry(string Name, string Json, string? Parent = null);

    private sealed class DictionaryLookup(List<Entry> entries) : IPresetLookup
    {
        private readonly Dictionary<string, Entry> _map = entries.ToDictionary(
            e => e.Name,
            StringComparer.OrdinalIgnoreCase
        );

        public PresetResolveRequest? FindByName(string name) =>
            _map.TryGetValue(name, out Entry? entry)
                ? new PresetResolveRequest(entry.Name, entry.Json, entry.Parent)
                : null;

        public void Replace(string name, string json, string? parent = null)
        {
            _map[name] = new(name, json, parent);
        }
    }

    private static EncodingProfile BuildBaseProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Base",
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideoOutput()],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

    private static VideoOutput BuildVideoOutput(int crf = 23) =>
        new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: crf,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
}
