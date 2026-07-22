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
    private static string Json(EncodingProfile p) => JsonConvert.SerializeObject(value: p);

    private class FakeLookup(Dictionary<Ulid, (string Json, Ulid? Parent)> presets) : IPresetLookup
    {
        public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid id) =>
            presets.TryGetValue(key: id, value: out (string Json, Ulid? Parent) entry)
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
        EncodingProfile root = MinimalProfile(name: "root");
        Ulid id = root.Id;
        FakeLookup lookup = new(presets: new() { [key: id] = (Json(p: root), null) });

        EncodingProfile resolved = PresetResolver.Resolve(presetId: id, lookup: lookup);
        resolved.Name.Should().Be(expected: "root");
    }

    [Fact]
    public void Child_overrides_parent_scalar()
    {
        EncodingProfile parent = MinimalProfile(name: "parent") with { SegmentDurationSeconds = 6 };
        Ulid parentId = parent.Id;
        Ulid childId = Ulid.NewUlid();
        string sparseChildJson = "{\"segmentDurationSeconds\": 4}";

        FakeLookup lookup = new(
            presets: new() { [key: parentId] = (Json(p: parent), null), [key: childId] = (sparseChildJson, parentId) }
        );

        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.SegmentDurationSeconds.Should().Be(expected: 4);
        resolved.Name.Should().Be(expected: "parent");
    }

    [Fact]
    public void Cycle_detected_throws()
    {
        Ulid a = Ulid.NewUlid();
        Ulid b = Ulid.NewUlid();
        EncodingProfile profileA = MinimalProfile(name: "a") with { Id = a };
        EncodingProfile profileB = MinimalProfile(name: "b") with { Id = b };
        FakeLookup lookup = new(presets: new() { [key: a] = (Json(p: profileA), b), [key: b] = (Json(p: profileB), a) });
        Action act = () => PresetResolver.Resolve(presetId: a, lookup: lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*cycle*");
    }

    [Fact]
    public void Missing_parent_throws()
    {
        Ulid childId = Ulid.NewUlid();
        Ulid orphanedParentId = Ulid.NewUlid();
        FakeLookup lookup = new(presets: new() { [key: childId] = ("{}", orphanedParentId) });
        Action act = () => PresetResolver.Resolve(presetId: childId, lookup: lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*not found*");
    }

    [Fact]
    public void Self_reference_detected_as_cycle()
    {
        Ulid id = Ulid.NewUlid();
        EncodingProfile profile = MinimalProfile(name: "self") with { Id = id };
        FakeLookup lookup = new(presets: new() { [key: id] = (Json(p: profile), id) });
        Action act = () => PresetResolver.Resolve(presetId: id, lookup: lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*cycle*");
    }

    [Fact]
    public void Eight_deep_chain_resolves()
    {
        Dictionary<Ulid, (string Json, Ulid? Parent)> presets = new();
        Ulid[] ids = [.. Enumerable.Range(start: 0, count: 8).Select(selector: _ => Ulid.NewUlid())];
        for (int i = 0; i < 8; i++)
        {
            Ulid? parent = i == 7 ? null : ids[i + 1];
            EncodingProfile p = MinimalProfile(name: $"level{i}") with { Id = ids[i] };
            presets[key: ids[i]] = (Json(p: p), parent);
        }
        FakeLookup lookup = new(presets: presets);
        Action act = () => PresetResolver.Resolve(presetId: ids[0], lookup: lookup);
        act.Should().NotThrow();
    }

    [Fact]
    public void Nine_deep_chain_exceeds_depth_limit()
    {
        Dictionary<Ulid, (string Json, Ulid? Parent)> presets = new();
        Ulid[] ids = [.. Enumerable.Range(start: 0, count: 9).Select(selector: _ => Ulid.NewUlid())];
        for (int i = 0; i < 9; i++)
        {
            Ulid? parent = i == 8 ? null : ids[i + 1];
            EncodingProfile p = MinimalProfile(name: $"level{i}") with { Id = ids[i] };
            presets[key: ids[i]] = (Json(p: p), parent);
        }
        FakeLookup lookup = new(presets: presets);
        Action act = () => PresetResolver.Resolve(presetId: ids[0], lookup: lookup);
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*max depth*");
    }

    [Fact]
    public void Sparse_empty_child_resolves_to_parent_unchanged()
    {
        EncodingProfile parent = MinimalProfile(name: "parent") with { SegmentDurationSeconds = 6 };
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            presets: new() { [key: parent.Id] = (Json(p: parent), null), [key: childId] = ("{}", parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.SegmentDurationSeconds.Should().Be(expected: 6);
        resolved.Name.Should().Be(expected: "parent");
    }

    [Fact]
    public void Override_at_object_field()
    {
        EncodingProfile parent = MinimalProfile(name: "parent") with
        {
            Hls = new() { CmafCompatible = true },
        };
        Ulid childId = Ulid.NewUlid();
        string childJson =
            "{\"hls\": {\"cmafCompatible\": false, \"playlistType\": 0, \"independentSegments\": true}}";
        FakeLookup lookup = new(
            presets: new() { [key: parent.Id] = (Json(p: parent), null), [key: childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.Hls!.CmafCompatible.Should().BeFalse();
    }

    [Fact]
    public void Override_at_array_field_replaces_opaquely()
    {
        EncodingProfile parent = MinimalProfile(name: "parent") with
        {
            Audio =
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: [],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "",
                    PlaylistNameTemplate: ""
                ),
            ],
        };
        Ulid childId = Ulid.NewUlid();
        string childJson = "{\"audio\": []}";
        FakeLookup lookup = new(
            presets: new() { [key: parent.Id] = (Json(p: parent), null), [key: childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.Audio.Should().BeEmpty();
    }

    [Fact]
    public void Override_at_enum_field()
    {
        EncodingProfile parent = MinimalProfile(name: "parent") with { Container = Container.HlsFmp4 };
        Ulid childId = Ulid.NewUlid();
        // Newtonsoft.Json serializes enums as integers by default; Mp4 = 1
        string childJson = $"{{\"container\": {(int)Container.Mp4}}}";
        FakeLookup lookup = new(
            presets: new() { [key: parent.Id] = (Json(p: parent), null), [key: childId] = (childJson, parent.Id) }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.Container.Should().Be(expected: Container.Mp4);
    }

    [Fact]
    public void Multi_level_chain_overrides_combine()
    {
        EncodingProfile grandparent = MinimalProfile(name: "gp") with { SegmentDurationSeconds = 8 };
        Ulid parentId = Ulid.NewUlid();
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            presets: new()
            {
                [key: grandparent.Id] = (Json(p: grandparent), null),
                [key: parentId] = ("{\"encodeMode\": 1}", grandparent.Id),
                [key: childId] = ("{\"autoDetectCrop\": true}", parentId),
            }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.SegmentDurationSeconds.Should().Be(expected: 8);
        resolved.EncodeMode.Should().Be(expected: EncodeMode.TwoPass);
        resolved.AutoDetectCrop.Should().BeTrue();
    }

    [Fact]
    public void Same_field_overridden_at_multiple_levels_child_wins()
    {
        EncodingProfile grandparent = MinimalProfile(name: "gp") with { SegmentDurationSeconds = 8 };
        Ulid parentId = Ulid.NewUlid();
        Ulid childId = Ulid.NewUlid();
        FakeLookup lookup = new(
            presets: new()
            {
                [key: grandparent.Id] = (Json(p: grandparent), null),
                [key: parentId] = ("{\"segmentDurationSeconds\": 6}", grandparent.Id),
                [key: childId] = ("{\"segmentDurationSeconds\": 4}", parentId),
            }
        );
        EncodingProfile resolved = PresetResolver.Resolve(presetId: childId, lookup: lookup);
        resolved.SegmentDurationSeconds.Should().Be(expected: 4);
    }

    [Fact]
    public void Concurrent_resolves_consistent()
    {
        EncodingProfile root = MinimalProfile(name: "root");
        FakeLookup lookup = new(presets: new() { [key: root.Id] = (Json(p: root), null) });
        EncodingProfile[] results = new EncodingProfile[64];
        Parallel.For(fromInclusive: 0, toExclusive: 64, body: i => results[i] = PresetResolver.Resolve(presetId: root.Id, lookup: lookup));
        results.Should().AllSatisfy(expected: r => r.Name.Should().Be(expected: "root"));
    }

    [Fact]
    public void Migration_invoked_for_older_schema_version()
    {
        PresetResolver.Migrations = [new TestV1ToV2Migration()];
        try
        {
            Ulid id = Ulid.NewUlid();
            EncodingProfile root = MinimalProfile(name: "v1") with { Id = id };
            Newtonsoft.Json.Linq.JObject json = Newtonsoft.Json.Linq.JObject.FromObject(o: root);
            json[propertyName: "schemaVersion"] = 1;
            FakeLookup lookup = new(presets: new() { [key: id] = (json.ToString(), null) });

            EncodingProfile resolved = PresetResolver.Resolve(presetId: id, lookup: lookup);
            resolved.Description.Should().Be(expected: "migrated:v1->v2");
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
            input[propertyName: "schemaVersion"] = 2;
            input[propertyName: "description"] = "migrated:v1->v2";
            return input;
        }
    }
}
