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

using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class ProfileDifferTests
{
    private static EncodingProfile Profile(string name = "p") =>
        new(
            Id: Ulid.NewUlid(),
            Name: name,
            Container: Container.HlsFmp4,
            Video: null,
            Audio: [],
            Subtitles: [],
            SegmentDurationSeconds: 6
        );

    [Fact]
    public void Identical_child_yields_empty_diff()
    {
        EncodingProfile parent = Profile();
        EncodingProfile child = parent with { };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff.Properties().Should().BeEmpty();
    }

    [Fact]
    public void Scalar_difference_appears_only_for_changed_field()
    {
        EncodingProfile parent = Profile();
        EncodingProfile child = parent with { SegmentDurationSeconds = 4 };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["segmentDurationSeconds"]!.Value<int>().Should().Be(4);
        diff.Properties().Should().HaveCount(1);
    }

    [Fact]
    public void Top_level_object_changed_field_yields_whole_object()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = false } };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["hls"].Should().NotBeNull();
        diff["hls"]!["cmafCompatible"]!.Value<bool>().Should().BeFalse();
        diff["hls"]!["independentSegments"]!.Value<bool>().Should().BeTrue();
    }

    [Fact]
    public void Array_change_replaces_whole_array()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            StreamPolicy.Transcode,
            AudioCodecType.Eac3,
            384,
            6,
            48000,
            [],
            null,
            null,
            null,
            "",
            ""
        );

        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [a1, a2] };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["audio"]!.Type.Should().Be(JTokenType.Array);
        ((JArray)diff["audio"]!).Count.Should().Be(2);
    }

    [Fact]
    public void Diff_then_apply_round_trip_yields_identity()
    {
        EncodingProfile parent = Profile() with
        {
            SegmentDurationSeconds = 6,
            Hls = new() { CmafCompatible = true },
        };
        EncodingProfile child = parent with
        {
            SegmentDurationSeconds = 4,
            Hls = new() { CmafCompatible = false, IndependentSegments = true },
        };

        JObject diff = ProfileDiffer.Diff(child, parent);

        JObject reconstructed = JObject.FromObject(parent);
        reconstructed.Merge(
            diff,
            new()
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge,
            }
        );
        EncodingProfile? recovered = reconstructed.ToObject<EncodingProfile>();

        recovered.Should().BeEquivalentTo(child);
    }

    [Fact]
    public void Object_null_in_parent_set_in_child_included()
    {
        EncodingProfile parent = Profile();
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = false } };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["hls"].Should().NotBeNull();
        diff["hls"]!.Type.Should().NotBe(JTokenType.Null);
    }

    [Fact]
    public void Object_set_in_parent_null_in_child_included_as_null()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = null };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["hls"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void Array_same_elements_different_order_replaces_whole_array()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            StreamPolicy.Transcode,
            AudioCodecType.Eac3,
            384,
            6,
            48000,
            [],
            null,
            null,
            null,
            "",
            ""
        );
        EncodingProfile parent = Profile() with { Audio = [a1, a2] };
        EncodingProfile child = parent with { Audio = [a2, a1] };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["audio"]!.Type.Should().Be(JTokenType.Array);
    }

    [Fact]
    public void Array_populated_in_parent_empty_in_child_included()
    {
        AudioOutput a1 = new(
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
        );
        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [] };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["audio"]!.Type.Should().Be(JTokenType.Array);
        ((JArray)diff["audio"]!).Count.Should().Be(0);
    }

    [Fact]
    public void Object_set_in_both_identical_omitted()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = true } };
        JObject diff = ProfileDiffer.Diff(child, parent);
        diff["hls"].Should().BeNull();
    }

    [Fact]
    public void Round_trip_with_array_change()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            StreamPolicy.Transcode,
            AudioCodecType.Eac3,
            384,
            6,
            48000,
            [],
            null,
            null,
            null,
            "",
            ""
        );
        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [a1, a2] };

        JObject diff = ProfileDiffer.Diff(child, parent);
        JObject reconstructed = JObject.FromObject(parent);
        reconstructed.Merge(
            diff,
            new()
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge,
            }
        );
        EncodingProfile? recovered = reconstructed.ToObject<EncodingProfile>();
        recovered.Should().BeEquivalentTo(child);
    }
}
