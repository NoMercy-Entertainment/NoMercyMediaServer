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
            "root",
            BaseProfileJson("root", 8000),
            null
        );
        FakeLookup lookup = new();

        EncodingProfile profile = _resolver.Resolve(root, lookup);

        profile.Name.Should().Be("root");
        profile.Video!.BitrateKbps.Should().Be(8000);
    }

    [Fact]
    public void Resolve_ChildOverridesParentBitrate()
    {
        PresetResolveRequest child = new(
            "child",
            BaseProfileJson("child", 6000),
            "parent"
        );
        FakeLookup lookup = new() { ["parent"] = new("parent", BaseProfileJson("parent", 12000)) };

        EncodingProfile profile = _resolver.Resolve(child, lookup);

        // Child's bitrate (6000) wins over parent's (12000).
        profile.Video!.BitrateKbps.Should().Be(6000);
    }

    [Fact]
    public void Resolve_ChildOmitsField_KeepsParentValue()
    {
        // Child profile only specifies the name + nothing else — populating an
        // EncodingProfile with that JSON should leave parent fields intact.
        PresetResolveRequest child = new(
            "child",
            "{\"Name\":\"child\"}",
            "parent"
        );
        FakeLookup lookup = new() { ["parent"] = new("parent", BaseProfileJson("parent", 9000)) };

        EncodingProfile profile = _resolver.Resolve(child, lookup);

        profile.Name.Should().Be("child");
        // Inherited from parent.
        profile.Video!.BitrateKbps.Should().Be(9000);
    }

    // ── Chain walking ───────────────────────────────────────────────────────

    [Fact]
    public void Resolve_ThreeLevelChain_AppliesEachOverrideInOrder()
    {
        FakeLookup lookup = new()
        {
            ["grandparent"] = new("grandparent", BaseProfileJson("grandparent", 4000)),
            ["parent"] = new(
                "parent",
                "{\"Video\":{\"BitrateKbps\":8000}}",
                "grandparent"
            ),
        };
        PresetResolveRequest child = new(
            "child",
            "{\"Name\":\"child\"}",
            "parent"
        );

        EncodingProfile profile = _resolver.Resolve(child, lookup);

        profile.Name.Should().Be("child");
        // Parent overrode grandparent's bitrate from 4000 → 8000.
        profile.Video!.BitrateKbps.Should().Be(8000);
    }

    // ── Error paths ─────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_MissingParent_Throws()
    {
        PresetResolveRequest leaf = new(
            "child",
            BaseProfileJson("child", 5000),
            "vanished_parent"
        );
        FakeLookup lookup = new();

        Action act = () => _resolver.Resolve(leaf, lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage("*vanished_parent*not found*");
    }

    [Fact]
    public void Resolve_Cycle_Throws()
    {
        // A → B → A
        FakeLookup lookup = new()
        {
            ["A"] = new("A", BaseProfileJson("A", 1000), "B"),
            ["B"] = new("B", BaseProfileJson("B", 2000), "A"),
        };
        PresetResolveRequest start = lookup["A"];

        Action act = () => _resolver.Resolve(start, lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle detected*");
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
            lookup[name] = new(name, BaseProfileJson(name, 1000 + i), parent);
        }
        PresetResolveRequest leaf = lookup["P0"];

        Action act = () => _resolver.Resolve(leaf, lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*levels*");
    }

    [Fact]
    public void Resolve_EmptyProfileJson_Throws()
    {
        PresetResolveRequest leaf = new("broken", "null");
        FakeLookup lookup = new();

        Action act = () => _resolver.Resolve(leaf, lookup);

        act.Should().Throw<InvalidOperationException>().WithMessage("*broken*invalid*");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string BaseProfileJson(string name, int bitrateKbps)
    {
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            name,
            Container.HlsFmp4,
            new(
                StreamPolicy.Transcode,
                NoMercy.Encoder.Codecs.VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Vbr,
                0,
                bitrateKbps,
                null,
                null,
                null,
                CodecProfile.Auto,
                null,
                null,
                8,
                null,
                2,
                false,
                "v",
                "p"
            ),
            [],
            []
        );
        return JsonConvert.SerializeObject(profile);
    }

    private sealed class FakeLookup : Dictionary<string, PresetResolveRequest>, INamePresetLookup
    {
        public FakeLookup()
            : base(StringComparer.OrdinalIgnoreCase) { }

        public PresetResolveRequest? FindByName(string name) =>
            TryGetValue(name, out PresetResolveRequest? entry) ? entry : null;
    }
}
