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

using Newtonsoft.Json;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Name-based preset inheritance resolver. Walks the parent chain via
/// human-readable names, deserialises the root profile, then overlays each
/// descendant's fields. Cycle + depth detection prevent runaway inheritance.
/// </summary>
public class ByNamePresetResolverTests
{
    private readonly ByNamePresetResolver _resolver = new();

    // ── Simple inheritance ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_NoParent_ReturnsRootProfile()
    {
        PresetResolveRequest root = new(
            Name: "root",
            ProfileJson: BaseProfileJson(name: "root", bitrateKbps: 8000),
            ParentName: null
        );
        FakeLookup lookup = new();

        EncodingProfile profile = _resolver.Resolve(request: root, lookup: lookup);

        profile.Name.Should().Be(expected: "root");
        profile.Video!.BitrateKbps.Should().Be(expected: 8000);
    }

    [Fact]
    public void Resolve_ChildOverridesParentBitrate()
    {
        PresetResolveRequest child = new(
            Name: "child",
            ProfileJson: BaseProfileJson(name: "child", bitrateKbps: 6000),
            ParentName: "parent"
        );
        FakeLookup lookup = new() { [key: "parent"] = new(Name: "parent", ProfileJson: BaseProfileJson(name: "parent", bitrateKbps: 12000)) };

        EncodingProfile profile = _resolver.Resolve(request: child, lookup: lookup);

        // Child's bitrate (6000) wins over parent's (12000).
        profile.Video!.BitrateKbps.Should().Be(expected: 6000);
    }

    [Fact]
    public void Resolve_ChildOmitsField_KeepsParentValue()
    {
        // Child profile only specifies the name + nothing else — populating an
        // EncodingProfile with that JSON should leave parent fields intact.
        PresetResolveRequest child = new(
            Name: "child",
            ProfileJson: "{\"Name\":\"child\"}",
            ParentName: "parent"
        );
        FakeLookup lookup = new() { [key: "parent"] = new(Name: "parent", ProfileJson: BaseProfileJson(name: "parent", bitrateKbps: 9000)) };

        EncodingProfile profile = _resolver.Resolve(request: child, lookup: lookup);

        profile.Name.Should().Be(expected: "child");
        // Inherited from parent.
        profile.Video!.BitrateKbps.Should().Be(expected: 9000);
    }

    // ── Chain walking ───────────────────────────────────────────────────────

    [Fact]
    public void Resolve_ThreeLevelChain_AppliesEachOverrideInOrder()
    {
        FakeLookup lookup = new()
        {
            [key: "grandparent"] = new(Name: "grandparent", ProfileJson: BaseProfileJson(name: "grandparent", bitrateKbps: 4000)),
            [key: "parent"] = new(
                Name: "parent",
                ProfileJson: "{\"Video\":{\"BitrateKbps\":8000}}",
                ParentName: "grandparent"
            ),
        };
        PresetResolveRequest child = new(
            Name: "child",
            ProfileJson: "{\"Name\":\"child\"}",
            ParentName: "parent"
        );

        EncodingProfile profile = _resolver.Resolve(request: child, lookup: lookup);

        profile.Name.Should().Be(expected: "child");
        // Parent overrode grandparent's bitrate from 4000 → 8000.
        profile.Video!.BitrateKbps.Should().Be(expected: 8000);
    }

    // ── Error paths ─────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_MissingParent_Throws()
    {
        PresetResolveRequest leaf = new(
            Name: "child",
            ProfileJson: BaseProfileJson(name: "child", bitrateKbps: 5000),
            ParentName: "vanished_parent"
        );
        FakeLookup lookup = new();

        Action act = () => _resolver.Resolve(request: leaf, lookup: lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*vanished_parent*not found*");
    }

    [Fact]
    public void Resolve_Cycle_Throws()
    {
        // A → B → A
        FakeLookup lookup = new()
        {
            [key: "A"] = new(Name: "A", ProfileJson: BaseProfileJson(name: "A", bitrateKbps: 1000), ParentName: "B"),
            [key: "B"] = new(Name: "B", ProfileJson: BaseProfileJson(name: "B", bitrateKbps: 2000), ParentName: "A"),
        };
        PresetResolveRequest start = lookup[key: "A"];

        Action act = () => _resolver.Resolve(request: start, lookup: lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*cycle detected*");
    }

    [Fact]
    public void Resolve_TooDeep_Throws()
    {
        // 12-level chain blows past MaxInheritanceDepth (10).
        FakeLookup lookup = new();
        for (int i = 0; i < 12; i++)
        {
            string name = $"P{i}";
            string? parent = i < 11 ? $"P{i + 1}" : null;
            lookup[key: name] = new(Name: name, ProfileJson: BaseProfileJson(name: name, bitrateKbps: 1000 + i), ParentName: parent);
        }
        PresetResolveRequest leaf = lookup[key: "P0"];

        Action act = () => _resolver.Resolve(request: leaf, lookup: lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*exceeds*levels*");
    }

    [Fact]
    public void Resolve_EmptyProfileJson_Throws()
    {
        PresetResolveRequest leaf = new(Name: "broken", ProfileJson: "null");
        FakeLookup lookup = new();

        Action act = () => _resolver.Resolve(request: leaf, lookup: lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*broken*invalid*");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string BaseProfileJson(string name, int bitrateKbps)
    {
        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: name,
            Container: Container.HlsFmp4,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: NoMercy.Encoder.Codecs.VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Vbr,
                Crf: 0,
                BitrateKbps: bitrateKbps,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: null,
                CodecProfile: CodecProfile.Auto,
                Level: null,
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "v",
                PlaylistNameTemplate: "p"
            ),
            Audio: [],
            Subtitles: []
        );
        return JsonConvert.SerializeObject(value: profile);
    }

    private sealed class FakeLookup : Dictionary<string, PresetResolveRequest>, INamePresetLookup
    {
        public FakeLookup()
            : base(comparer: StringComparer.OrdinalIgnoreCase) { }

        public PresetResolveRequest? FindByName(string name) =>
            TryGetValue(key: name, value: out PresetResolveRequest? entry) ? entry : null;
    }
}
