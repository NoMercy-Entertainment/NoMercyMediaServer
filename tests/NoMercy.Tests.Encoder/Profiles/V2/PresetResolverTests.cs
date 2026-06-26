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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class PresetResolverTests
{
    private static string Json(EncodingProfile p) => JsonConvert.SerializeObject(p);

    private class FakeLookup(Dictionary<Ulid, (string Json, Ulid? Parent)> presets) : IPresetLookup
    {
        public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid id) =>
            presets.TryGetValue(id, out (string Json, Ulid? Parent) entry)
                ? (entry.Json, entry.Parent)
                : null;
    }

    private static EncodingProfile MinimalProfile(string name) =>
        new(
            Id: Ulid.NewUlid(),
            Name: name,
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: []
        );

    [Fact]
    public void Resolves_root_with_no_parent_chain()
    {
        EncodingProfile root = MinimalProfile("root");
        Ulid id = root.Id;
        FakeLookup lookup = new(new() { [id] = (Json(root), null) });

        EncodingProfile resolved = PresetResolver.Resolve(id, lookup);
        resolved.Name.Should().Be("root");
    }

    [Fact]
    public void Child_overrides_parent_scalar()
    {
        EncodingProfile parent = MinimalProfile("parent") with { SegmentDurationSeconds = 6 };
        Ulid parentId = parent.Id;
        Ulid childId = Ulid.NewUlid();
        string sparseChildJson = "{\"segmentDurationSeconds\": 4}";

        FakeLookup lookup = new(
            new() { [parentId] = (Json(parent), null), [childId] = (sparseChildJson, parentId) }
        );

        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.SegmentDurationSeconds.Should().Be(4);
        resolved.Name.Should().Be("parent");
    }

    [Fact]
    public void Cycle_detected_throws()
    {
        Ulid a = Ulid.NewUlid();
        Ulid b = Ulid.NewUlid();
        EncodingProfile profileA = MinimalProfile("a") with { Id = a };
        EncodingProfile profileB = MinimalProfile("b") with { Id = b };
        FakeLookup lookup = new(new() { [a] = (Json(profileA), b), [b] = (Json(profileB), a) });
        Action act = () => PresetResolver.Resolve(a, lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Missing_parent_throws()
    {
        Ulid childId = Ulid.NewUlid();
        Ulid orphanedParentId = Ulid.NewUlid();
        FakeLookup lookup = new(new() { [childId] = ("{}", orphanedParentId) });
        Action act = () => PresetResolver.Resolve(childId, lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public void Self_reference_detected_as_cycle()
    {
        Ulid id = Ulid.NewUlid();
        EncodingProfile profile = MinimalProfile("self") with { Id = id };
        FakeLookup lookup = new(new() { [id] = (Json(profile), id) });
        Action act = () => PresetResolver.Resolve(id, lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Eight_deep_chain_resolves()
    {
        Dictionary<Ulid, (string Json, Ulid? Parent)> presets = new();
        Ulid[] ids = [.. Enumerable.Range(0, 8).Select(_ => Ulid.NewUlid())];
        for (int i = 0; i < 8; i++)
        {
            Ulid? parent = i == 7 ? null : ids[i + 1];
            EncodingProfile p = MinimalProfile($"level{i}") with { Id = ids[i] };
            presets[ids[i]] = (Json(p), parent);
        }
        FakeLookup lookup = new(presets);
        Action act = () => PresetResolver.Resolve(ids[0], lookup);
        act.Should().NotThrow();
    }

    [Fact]
    public void Nine_deep_chain_exceeds_depth_limit()
    {
        Dictionary<Ulid, (string Json, Ulid? Parent)> presets = new();
        Ulid[] ids = [.. Enumerable.Range(0, 9).Select(_ => Ulid.NewUlid())];
        for (int i = 0; i < 9; i++)
        {
            Ulid? parent = i == 8 ? null : ids[i + 1];
            EncodingProfile p = MinimalProfile($"level{i}") with { Id = ids[i] };
            presets[ids[i]] = (Json(p), parent);
        }
        FakeLookup lookup = new(presets);
        Action act = () => PresetResolver.Resolve(ids[0], lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage("*max depth*");
    }

    [Fact]
    public void Sparse_empty_child_resolves_to_parent_unchanged()
    {
        EncodingProfile parent = MinimalProfile("parent") with { SegmentDurationSeconds = 6 };
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            new() { [parent.Id] = (Json(parent), null), [childId] = ("{}", parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.SegmentDurationSeconds.Should().Be(6);
        resolved.Name.Should().Be("parent");
    }

    [Fact]
    public void Override_at_object_field()
    {
        EncodingProfile parent = MinimalProfile("parent") with
        {
            Hls = new() { CmafCompatible = true },
        };
        Ulid childId = Ulid.NewUlid();
        string childJson =
            "{\"hls\": {\"cmafCompatible\": false, \"playlistType\": 0, \"independentSegments\": true}}";
        FakeLookup lookup = new(
            new() { [parent.Id] = (Json(parent), null), [childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.Hls!.CmafCompatible.Should().BeFalse();
    }

    [Fact]
    public void Override_at_array_field_replaces_opaquely()
    {
        EncodingProfile parent = MinimalProfile("parent") with
        {
            Audio =
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    "",
                    ""
                ),
            ],
        };
        Ulid childId = Ulid.NewUlid();
        string childJson = "{\"audio\": []}";
        FakeLookup lookup = new(
            new() { [parent.Id] = (Json(parent), null), [childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.Audio.Should().BeEmpty();
    }

    [Fact]
    public void Override_at_enum_field()
    {
        EncodingProfile parent = MinimalProfile("parent") with { Container = Container.HlsFmp4 };
        Ulid childId = Ulid.NewUlid();
        // Newtonsoft.Json serializes enums as integers by default; Mp4 = 1
        string childJson = $"{{\"container\": {(int)Container.Mp4}}}";
        FakeLookup lookup = new(
            new() { [parent.Id] = (Json(parent), null), [childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.Container.Should().Be(Container.Mp4);
    }

    [Fact]
    public void Multi_level_chain_overrides_combine()
    {
        EncodingProfile grandparent = MinimalProfile("gp") with { SegmentDurationSeconds = 8 };
        Ulid parentId = Ulid.NewUlid();
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            new()
            {
                [grandparent.Id] = (Json(grandparent), null),
                [parentId] = ("{\"encodeMode\": 1}", grandparent.Id),
                [childId] = ("{\"autoDetectCrop\": true}", parentId),
            }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.SegmentDurationSeconds.Should().Be(8);
        resolved.EncodeMode.Should().Be(EncodeMode.TwoPass);
        resolved.AutoDetectCrop.Should().BeTrue();
    }

    [Fact]
    public void Same_field_overridden_at_multiple_levels_child_wins()
    {
        EncodingProfile grandparent = MinimalProfile("gp") with { SegmentDurationSeconds = 8 };
        Ulid parentId = Ulid.NewUlid();
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            new()
            {
                [grandparent.Id] = (Json(grandparent), null),
                [parentId] = ("{\"segmentDurationSeconds\": 6}", grandparent.Id),
                [childId] = ("{\"segmentDurationSeconds\": 4}", parentId),
            }
        );
        EncodingProfile resolved = PresetResolver.Resolve(childId, lookup);
        resolved.SegmentDurationSeconds.Should().Be(4);
    }

    [Fact]
    public void Concurrent_resolves_consistent()
    {
        EncodingProfile root = MinimalProfile("root");
        FakeLookup lookup = new(new() { [root.Id] = (Json(root), null) });
        EncodingProfile[] results = new EncodingProfile[64];
        Parallel.For(0, 64, i => results[i] = PresetResolver.Resolve(root.Id, lookup));
        results.Should().AllSatisfy(r => r.Name.Should().Be("root"));
    }

    [Fact]
    public void Migration_invoked_for_older_schema_version()
    {
        PresetResolver.Migrations = [new TestV1ToV2Migration()];
        try
        {
            Ulid id = Ulid.NewUlid();
            EncodingProfile root = MinimalProfile("v1") with { Id = id };
            Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.FromObject(root);
            json["schemaVersion"] = 1;
            FakeLookup lookup = new(new() { [id] = (json.ToString(), null) });

            EncodingProfile resolved = PresetResolver.Resolve(id, lookup);
            resolved.Description.Should().Be("migrated:v1->v2");
        }
        finally
        {
            PresetResolver.Migrations = [];
        }
    }

    private sealed class TestV1ToV2Migration : IProfileMigration
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public Newtonsoft.Json.Linq.JObject Migrate(Newtonsoft.Json.Linq.JObject input)
        {
            input["schemaVersion"] = 2;
            input["description"] = "migrated:v1->v2";
            return input;
        }
    }
}
