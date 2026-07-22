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

public class SubtitleAcquisitionConfigTests
{
    [Fact]
    public void DefaultConstructed_HasSpecDefaults()
    {
        SubtitleAcquisitionConfig config = new();

        config.Enabled.Should().BeFalse();
        config.Providers.Should().BeEquivalentTo(expectation: [SubtitleProvider.OpenSubtitles]);
        config.Languages.Should().BeEmpty();
        config.Strategy.Should().Be(expected: SubtitleMatchStrategy.HashThenFilenameThenTitle);
        config.MaxPerLanguage.Should().Be(expected: 1);
        config.MinRating.Should().Be(expected: 0.0);
        config.MinDownloads.Should().Be(expected: 0);
        config.TrustedUploadersOnly.Should().BeFalse();
        config.RequireMatchingFps.Should().BeFalse();
        config.PerRequestTimeout.Should().Be(expected: TimeSpan.FromSeconds(seconds: 5));
        config.FillMissingOnly.Should().BeTrue();
        config.EmbedPolicy.Should().Be(expected: SubtitleEmbedPolicy.ExactMatchOnly);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        SubtitleAcquisitionConfig original = new()
        {
            Enabled = true,
            Providers = [SubtitleProvider.OpenSubtitles],
            Languages = ["en", "nl"],
            Strategy = SubtitleMatchStrategy.HashOnly,
            MaxPerLanguage = 3,
            MinRating = 7.5,
            MinDownloads = 100,
            TrustedUploadersOnly = true,
            RequireMatchingFps = true,
            PerRequestTimeout = TimeSpan.FromSeconds(seconds: 10),
            FillMissingOnly = false,
            EmbedPolicy = SubtitleEmbedPolicy.AlwaysSidecar,
        };

        string json = JsonConvert.SerializeObject(value: original);
        SubtitleAcquisitionConfig? restored =
            JsonConvert.DeserializeObject<SubtitleAcquisitionConfig>(value: json);

        restored.Should().NotBeNull();
        restored!.Enabled.Should().BeTrue();
        restored.Languages.Should().BeEquivalentTo(expectation: ["en", "nl"]);
        restored.Strategy.Should().Be(expected: SubtitleMatchStrategy.HashOnly);
        restored.MaxPerLanguage.Should().Be(expected: 3);
        restored.MinRating.Should().Be(expected: 7.5);
        restored.MinDownloads.Should().Be(expected: 100);
        restored.TrustedUploadersOnly.Should().BeTrue();
        restored.RequireMatchingFps.Should().BeTrue();
        restored.PerRequestTimeout.Should().Be(expected: TimeSpan.FromSeconds(seconds: 10));
        restored.FillMissingOnly.Should().BeFalse();
        restored.EmbedPolicy.Should().Be(expected: SubtitleEmbedPolicy.AlwaysSidecar);
    }

    [Fact]
    public void EncodingProfile_SubtitleAcquisition_DefaultsToNull()
    {
        EncodingProfile profile = new(Id: Ulid.NewUlid(), Name: "Test", Container: Container.Mkv, Video: null, Audio: [], Subtitles: []);

        profile.SubtitleAcquisition.Should().BeNull();
    }
}
