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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class SubtitleAcquisitionValidatorTests
{
    private static SubtitleOutput WebVttOutput() =>
        new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: ["en"],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );

    private static EncodingProfile VideoProfile(
        Container container = Container.Mkv,
        SubtitleOutput[]? subtitles = null,
        SubtitleAcquisitionConfig? acquisition = null
    ) =>
        new EncodingProfile(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: container,
            Video: null,
            Audio: [],
            Subtitles: subtitles ?? []
        ) with
        {
            SubtitleAcquisition = acquisition,
        };

    private static EncodingProfile AudioOnlyProfile(
        Container container,
        SubtitleAcquisitionConfig? acquisition = null
    ) =>
        new EncodingProfile(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: container,
            Video: null,
            Audio: [],
            Subtitles: []
        ) with
        {
            SubtitleAcquisition = acquisition,
        };

    // Rule 1: Enabled=true + empty Subtitles[] → reject
    [Fact]
    public void EnabledWithNoSubtitleOutputs_IsRejected()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("SubtitleAcquisition requires at least one declared subtitle output")
            );
    }

    // Rule 1: Enabled=true + has subtitle outputs → no rejection from this rule
    [Fact]
    public void EnabledWithSubtitleOutputs_NoRejectionFromRule1()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e =>
                e.Contains("SubtitleAcquisition requires at least one declared subtitle output")
            );
    }

    // Rule 2: Enabled=true + audio-only container → reject
    [Theory]
    [InlineData(Container.Mp3)]
    [InlineData(Container.Aac)]
    [InlineData(Container.Flac)]
    [InlineData(Container.Ogg)]
    [InlineData(Container.Mka)]
    public void EnabledOnAudioOnlyContainer_IsRejected(Container container)
    {
        EncodingProfile profile = AudioOnlyProfile(
            container,
            acquisition: new SubtitleAcquisitionConfig { Enabled = true }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e =>
                e.Contains("SubtitleAcquisition is incompatible with audio-only containers")
            );
    }

    // Rule 3: MinRating < 0 → reject
    [Fact]
    public void MinRatingBelowZero_IsRejected()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true, MinRating = -0.1 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("SubtitleAcquisition.MinRating must be in [0, 10]"));
    }

    // Rule 3: MinRating > 10 → reject
    [Fact]
    public void MinRatingAboveTen_IsRejected()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true, MinRating = 10.1 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("SubtitleAcquisition.MinRating must be in [0, 10]"));
    }

    // Rule 3: MinRating = 0 → no error from this rule
    [Fact]
    public void MinRatingZero_NoRatingError()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true, MinRating = 0.0 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result
            .Errors.Should()
            .NotContain(e => e.Contains("SubtitleAcquisition.MinRating must be in [0, 10]"));
    }

    // Rule 4: MaxPerLanguage = 0 → reject
    [Fact]
    public void MaxPerLanguageZero_IsRejected()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true, MaxPerLanguage = 0 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("SubtitleAcquisition.MaxPerLanguage must be at least 1"));
    }

    // Rule 4: MaxPerLanguage < 0 → reject
    [Fact]
    public void MaxPerLanguageNegative_IsRejected()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig { Enabled = true, MaxPerLanguage = -1 }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.Contains("SubtitleAcquisition.MaxPerLanguage must be at least 1"));
    }

    // Rule 5: EmbedPolicy=ExactMatchOnly + TitleOnly → warn, not error
    [Fact]
    public void ExactMatchOnlyPlusTitleOnly_EmitsWarningNotError()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [WebVttOutput()],
            acquisition: new SubtitleAcquisitionConfig
            {
                Enabled = true,
                EmbedPolicy = SubtitleEmbedPolicy.ExactMatchOnly,
                Strategy = SubtitleMatchStrategy.TitleOnly,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("TitleOnly"));
        result
            .Warnings.Should()
            .Contain(w => w.Contains("TitleOnly") && w.Contains("ExactMatchOnly"));
    }

    // Null acquisition → no acquisition errors at all
    [Fact]
    public void NullAcquisitionConfig_NoAcquisitionErrors()
    {
        EncodingProfile profile = VideoProfile(subtitles: [], acquisition: null);

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("SubtitleAcquisition"));
        result.Warnings.Should().NotContain(w => w.Contains("SubtitleAcquisition"));
    }

    // Enabled=false → no acquisition validation at all
    [Fact]
    public void DisabledAcquisition_SkipsAllValidation()
    {
        EncodingProfile profile = VideoProfile(
            subtitles: [],
            acquisition: new SubtitleAcquisitionConfig
            {
                Enabled = false,
                MaxPerLanguage = 0,
                MinRating = -5,
            }
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Contains("SubtitleAcquisition"));
    }
}
