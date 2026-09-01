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

using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Streaming.Dtos;
using NoMercy.Api.Services;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.LiveTranscode;
using Xunit;

namespace NoMercy.Tests.Api;

// RULE: start-session/new-shape-payload-not-rejected — a per-codec client_caps
// payload (video/audio/supported_containers, no legacy video_codecs/
// audio_codecs/containers) must deserialize and validate cleanly. Regression
// for a live production 400 ("VideoCodecs/AudioCodecs/Containers field is
// required") caused by ClientCapabilitiesDto still declaring the legacy
// fields as non-nullable, so ASP.NET's implicit required-on-non-nullable-
// reference-type validation rejected every new-shape client before the
// request ever reached PlaybackDecisionEngine.
[Trait("Category", "EncoderValidationContract")]
public class StartLiveSessionRequestNewShapeTests
{
    // The exact payload shape reported failing in production.
    private const string NewShapeOnlyRequestJson = """
        {
          "video_file_id": "01M1BERBHT0EQD80FNS0G3PWDJ",
          "client_caps": {
            "video": [
              { "codec": "H264", "profiles": ["main", "high", "high10"], "max_bit_depth": 10,
                "max_width": 7680, "max_height": 4320, "max_framerate": 60,
                "hdr_formats": [], "max_bitrate_kbps": 0 }
            ],
            "audio": [
              { "codec": "AAC", "max_channels": 2, "passthrough": false, "decode": true },
              { "codec": "FLAC", "max_channels": 2, "passthrough": false, "decode": true },
              { "codec": "Opus", "max_channels": 2, "passthrough": false, "decode": true },
              { "codec": "MP3", "max_channels": 2, "passthrough": false, "decode": true }
            ],
            "supported_containers": ["HLS"],
            "supports_hdr": false,
            "max_bitrate_kbps": 0,
            "max_audio_channels": 2
          },
          "start_time_seconds": 225
        }
        """;

    [Fact]
    public void NewShapeOnlyPayload_DeserializesWithoutError()
    {
        StartLiveSessionRequest? request = JsonConvert.DeserializeObject<StartLiveSessionRequest>(
            NewShapeOnlyRequestJson
        );

        Assert.NotNull(request);
        Assert.Equal("01M1BERBHT0EQD80FNS0G3PWDJ", request!.VideoFileId);
        Assert.NotNull(request.ClientCaps.Video);
        Assert.Single(request.ClientCaps.Video!);
        Assert.Equal(4, request.ClientCaps.Audio!.Length);
    }

    [Fact]
    public void NewShapeOnlyPayload_PassesDataAnnotationsValidation()
    {
        StartLiveSessionRequest request = JsonConvert.DeserializeObject<StartLiveSessionRequest>(
            NewShapeOnlyRequestJson
        )!;

        List<ValidationResult> results = [];
        bool isValid = Validator.TryValidateObject(
            request.ClientCaps,
            new(request.ClientCaps),
            results,
            validateAllProperties: true
        );

        Assert.True(
            isValid,
            $"New-shape-only client_caps must pass validation. Errors: "
                + string.Join(", ", results.Select(r => r.ErrorMessage))
        );
    }

    [Fact]
    public void NewShapeOnlyPayload_MapsToPerCodecClientCapabilities()
    {
        StartLiveSessionRequest request = JsonConvert.DeserializeObject<StartLiveSessionRequest>(
            NewShapeOnlyRequestJson
        )!;

        ClientCapabilities caps = LiveTranscodeService.ToClientCapabilities(request.ClientCaps);

        Assert.Single(caps.Video);
        Assert.Equal(VideoCodecType.H264, caps.Video[0].Codec);
        Assert.Equal(10, caps.Video[0].MaxBitDepth);
        Assert.Contains("high10", caps.Video[0].Profiles);

        Assert.Equal(4, caps.Audio.Length);
        Assert.Contains(caps.Audio, a => a.Codec == AudioCodecType.Aac);

        // Legacy fields absent from this payload — must not be synthesized as
        // populated arrays, so PlaybackDecisionEngine's legacy-payload
        // synthesis fallback correctly no-ops and uses the per-codec data.
        Assert.Null(caps.SupportedVideoCodecs);
        Assert.Null(caps.SupportedAudioCodecs);
    }

    [Fact]
    public void LegacyOnlyPayload_StillPassesValidation()
    {
        const string legacyJson = """
            {
              "video_file_id": "01M1BERBHT0EQD80FNS0G3PWDJ",
              "client_caps": {
                "video_codecs": ["H264"],
                "audio_codecs": ["AAC"],
                "containers": ["HLS"],
                "max_width": 1920,
                "max_height": 1080,
                "supports_hdr": false,
                "supports_10bit": false,
                "max_bitrate_kbps": 0
              },
              "start_time_seconds": 0
            }
            """;

        StartLiveSessionRequest? request = JsonConvert.DeserializeObject<StartLiveSessionRequest>(
            legacyJson
        );

        Assert.NotNull(request);
        List<ValidationResult> results = [];
        bool isValid = Validator.TryValidateObject(
            request!.ClientCaps,
            new(request.ClientCaps),
            results,
            validateAllProperties: true
        );

        Assert.True(
            isValid,
            $"Older client builds sending only the legacy flat shape must still pass "
                + $"validation. Errors: {string.Join(", ", results.Select(r => r.ErrorMessage))}"
        );

        ClientCapabilities caps = LiveTranscodeService.ToClientCapabilities(request.ClientCaps);
        Assert.Equal([VideoCodecType.H264], caps.SupportedVideoCodecs);
        Assert.Empty(caps.Video);

        // Regression: the legacy wire field is "containers", not
        // "supported_containers" — reading only the primary field silently
        // dropped every legacy client's container list, made
        // IsContainerCompatible always false, and forced a Remux/transcode
        // session even when video/audio/HDR/10-bit all matched.
        Assert.Equal(["HLS"], caps.SupportedContainers);
    }
}
