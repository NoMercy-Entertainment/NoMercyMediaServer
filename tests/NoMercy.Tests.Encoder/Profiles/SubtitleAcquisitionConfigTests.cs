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
        config.Providers.Should().BeEquivalentTo([SubtitleProvider.OpenSubtitles]);
        config.Languages.Should().BeEmpty();
        config.Strategy.Should().Be(SubtitleMatchStrategy.HashThenFilenameThenTitle);
        config.MaxPerLanguage.Should().Be(1);
        config.MinRating.Should().Be(0.0);
        config.MinDownloads.Should().Be(0);
        config.TrustedUploadersOnly.Should().BeFalse();
        config.RequireMatchingFps.Should().BeFalse();
        config.PerRequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        config.FillMissingOnly.Should().BeTrue();
        config.EmbedPolicy.Should().Be(SubtitleEmbedPolicy.ExactMatchOnly);
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
            PerRequestTimeout = TimeSpan.FromSeconds(10),
            FillMissingOnly = false,
            EmbedPolicy = SubtitleEmbedPolicy.AlwaysSidecar,
        };

        string json = JsonConvert.SerializeObject(original);
        SubtitleAcquisitionConfig? restored =
            JsonConvert.DeserializeObject<SubtitleAcquisitionConfig>(json);

        restored.Should().NotBeNull();
        restored!.Enabled.Should().BeTrue();
        restored.Languages.Should().BeEquivalentTo(["en", "nl"]);
        restored.Strategy.Should().Be(SubtitleMatchStrategy.HashOnly);
        restored.MaxPerLanguage.Should().Be(3);
        restored.MinRating.Should().Be(7.5);
        restored.MinDownloads.Should().Be(100);
        restored.TrustedUploadersOnly.Should().BeTrue();
        restored.RequireMatchingFps.Should().BeTrue();
        restored.PerRequestTimeout.Should().Be(TimeSpan.FromSeconds(10));
        restored.FillMissingOnly.Should().BeFalse();
        restored.EmbedPolicy.Should().Be(SubtitleEmbedPolicy.AlwaysSidecar);
    }

    [Fact]
    public void EncodingProfile_SubtitleAcquisition_DefaultsToNull()
    {
        EncodingProfile profile = new(Ulid.NewUlid(), "Test", Container.Mkv, null, [], []);

        profile.SubtitleAcquisition.Should().BeNull();
    }
}
