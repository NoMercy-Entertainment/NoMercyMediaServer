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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

public class ValidateStageTests
{
    private readonly ValidateStage _stage = new(NullLogger<ValidateStage>.Instance);
    private readonly EncodingContext _context = EncodingContext.Create();

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile BuildValidProfile() =>
        new(
            Ulid.NewUlid(),
            "Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H264,
                1920,
                1080,
                RateControlMode.Crf,
                23,
                4000,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.1",
                null,
                8,
                null,
                2,
                false,
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    192,
                    2,
                    48000,
                    ["en"],
                    null,
                    null,
                    null,
                    ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            []
        );

    private static EncodingProfile BuildInvalidProfile() =>
        new(
            Ulid.NewUlid(),
            "Invalid",
            Container.HlsTs,
            null,
            [
                new(
                    StreamPolicy.Transcode,
                    AudioCodecType.Aac,
                    0,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            []
        );

    // ------------------------------------------------------------------
    // Valid profile → success, passes input through
    // ------------------------------------------------------------------

    [Fact]
    public async Task ValidProfile_ReturnsSuccess_WithPassthrough()
    {
        EncodingProfile profile = BuildValidProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
        StageSuccess<ValidateInput> success = (StageSuccess<ValidateInput>)result;
        success.Value.Profile.Should().Be(profile);
        success.Value.Media.Should().Be(media);
    }

    // ------------------------------------------------------------------
    // Invalid profile (audio BitrateKbps = 0) → ProfileInvalid failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvalidProfile_WithErrors_ReturnsProfileInvalidFailure()
    {
        EncodingProfile profile = BuildInvalidProfile();
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
        failure.Error.StageName.Should().Be("Validate");
        failure.Error.Message.Should().Contain("BitrateKbps");
    }

    // ------------------------------------------------------------------
    // Profile with no audio/video → still valid (nothing to validate)
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithNoOutputs_ReturnsSuccess()
    {
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Empty",
            Container.HlsTs,
            null,
            [],
            []
        );
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageSuccess<ValidateInput>>();
    }

    // ------------------------------------------------------------------
    // Profile with incompatible codec → ProfileInvalid failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task ProfileWithIncompatibleCodec_ReturnsFailure()
    {
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Bad Codec",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                RateControlMode.Crf,
                23,
                4000,
                null,
                null,
                "medium",
                CodecProfile.Main,
                "4.1",
                null,
                8,
                null,
                2,
                false,
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            [],
            []
        );
        MediaInfo media = BuildMediaInfo();
        ValidateInput input = new(media, profile);

        StageResult result = await _stage.ExecuteAsync(input, _context, default);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProfileInvalid);
    }
}
