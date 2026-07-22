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
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff.Properties().Should().BeEmpty();
    }

    [Fact]
    public void Scalar_difference_appears_only_for_changed_field()
    {
        EncodingProfile parent = Profile();
        EncodingProfile child = parent with { SegmentDurationSeconds = 4 };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "segmentDurationSeconds"]!.Value<int>().Should().Be(expected: 4);
        diff.Properties().Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Top_level_object_changed_field_yields_whole_object()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = false } };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "hls"].Should().NotBeNull();
        diff[propertyName: "hls"]![key: "cmafCompatible"]!.Value<bool>().Should().BeFalse();
        diff[propertyName: "hls"]![key: "independentSegments"]!.Value<bool>().Should().BeTrue();
    }

    [Fact]
    public void Array_change_replaces_whole_array()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "",
            PlaylistNameTemplate: ""
        );

        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [a1, a2] };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "audio"]!.Type.Should().Be(expected: JTokenType.Array);
        ((JArray)diff[propertyName: "audio"]!).Count.Should().Be(expected: 2);
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

        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);

        JObject reconstructed = JObject.FromObject(o: parent);
        reconstructed.Merge(
            content: diff,
            settings: new()
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge,
            }
        );
        EncodingProfile? recovered = reconstructed.ToObject<EncodingProfile>();

        recovered.Should().BeEquivalentTo(expectation: child);
    }

    [Fact]
    public void Object_null_in_parent_set_in_child_included()
    {
        EncodingProfile parent = Profile();
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = false } };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "hls"].Should().NotBeNull();
        diff[propertyName: "hls"]!.Type.Should().NotBe(unexpected: JTokenType.Null);
    }

    [Fact]
    public void Object_set_in_parent_null_in_child_included_as_null()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = null };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "hls"]!.Type.Should().Be(expected: JTokenType.Null);
    }

    [Fact]
    public void Array_same_elements_different_order_replaces_whole_array()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "",
            PlaylistNameTemplate: ""
        );
        EncodingProfile parent = Profile() with { Audio = [a1, a2] };
        EncodingProfile child = parent with { Audio = [a2, a1] };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "audio"]!.Type.Should().Be(expected: JTokenType.Array);
    }

    [Fact]
    public void Array_populated_in_parent_empty_in_child_included()
    {
        AudioOutput a1 = new(
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
        );
        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [] };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "audio"]!.Type.Should().Be(expected: JTokenType.Array);
        ((JArray)diff[propertyName: "audio"]!).Count.Should().Be(expected: 0);
    }

    [Fact]
    public void Object_set_in_both_identical_omitted()
    {
        EncodingProfile parent = Profile() with { Hls = new() { CmafCompatible = true } };
        EncodingProfile child = parent with { Hls = new() { CmafCompatible = true } };
        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        diff[propertyName: "hls"].Should().BeNull();
    }

    [Fact]
    public void Round_trip_with_array_change()
    {
        AudioOutput a1 = new(
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
        );
        AudioOutput a2 = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Eac3,
            BitrateKbps: 384,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "",
            PlaylistNameTemplate: ""
        );
        EncodingProfile parent = Profile() with { Audio = [a1] };
        EncodingProfile child = parent with { Audio = [a1, a2] };

        JObject diff = ProfileDiffer.Diff(child: child, resolvedParent: parent);
        JObject reconstructed = JObject.FromObject(o: parent);
        reconstructed.Merge(
            content: diff,
            settings: new()
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge,
            }
        );
        EncodingProfile? recovered = reconstructed.ToObject<EncodingProfile>();
        recovered.Should().BeEquivalentTo(expectation: child);
    }
}
