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
using NoMercy.Encoder.Analysis;

namespace NoMercy.Tests.Encoder.Analysis;

/// <summary>
/// Exercises the Dolby Vision extraction from ffprobe's <c>side_data_list</c>.
/// Two spellings exist in the wild ("DOVI configuration record" for
/// container-level records, "Dolby Vision Metadata" for in-stream RPUs);
/// both must parse. Non-DV files must return null rather than a zeroed
/// <see cref="DolbyVisionInfo"/> so downstream code can fast-path.
/// </summary>
public class DolbyVisionParsingTests
{
    [Fact]
    public void Parse_DoviConfigurationRecord_Profile8_1_ReturnsHdr10Compat()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "side_data_list": [
                    {
                      "side_data_type": "DOVI configuration record",
                      "dv_version_major": 1,
                      "dv_profile": 8,
                      "dv_level": 6,
                      "rpu_present_flag": 1,
                      "el_present_flag": 0,
                      "bl_present_flag": 1,
                      "dv_bl_signal_compatibility_id": 1
                    }
                  ]
                }
              ]
            }
            """;

        DolbyVisionInfo? dv = ExtractDv(json);

        dv.Should().NotBeNull();
        dv!.Profile.Should().Be(8);
        dv.Level.Should().Be(6);
        dv.HasRpu.Should().BeTrue();
        dv.HasEl.Should().BeFalse();
        dv.BlCompat.Should().Be(DvBlCompatibility.Hdr10);
    }

    [Fact]
    public void Parse_DolbyVisionMetadata_InStreamRpu_Matches()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "side_data_list": [
                    {
                      "side_data_type": "Dolby Vision Metadata",
                      "dv_profile": 7,
                      "dv_level": 5,
                      "rpu_present_flag": 1,
                      "el_present_flag": 1,
                      "dv_bl_signal_compatibility_id": 0
                    }
                  ]
                }
              ]
            }
            """;

        DolbyVisionInfo? dv = ExtractDv(json);

        dv.Should().NotBeNull();
        dv!.Profile.Should().Be(7);
        dv.HasEl.Should().BeTrue();
        dv.BlCompat.Should().Be(DvBlCompatibility.None);
    }

    [Fact]
    public void Parse_Profile8_2_ReturnsSdrCompat()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "side_data_list": [
                    {
                      "side_data_type": "DOVI configuration record",
                      "dv_profile": 8,
                      "dv_level": 9,
                      "rpu_present_flag": 1,
                      "dv_bl_signal_compatibility_id": 2
                    }
                  ]
                }
              ]
            }
            """;

        DolbyVisionInfo? dv = ExtractDv(json);

        dv!.BlCompat.Should().Be(DvBlCompatibility.Sdr);
    }

    [Fact]
    public void Parse_NoSideDataList_ReturnsNull()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "hevc" }
              ]
            }
            """;

        ExtractDv(json).Should().BeNull();
    }

    [Fact]
    public void Parse_SideDataWithoutDovi_ReturnsNull()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "side_data_list": [
                    { "side_data_type": "Mastering display metadata" }
                  ]
                }
              ]
            }
            """;

        ExtractDv(json).Should().BeNull();
    }

    [Fact]
    public void Parse_AudioStreamWithSideData_Ignored()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "audio",
                  "side_data_list": [
                    { "side_data_type": "DOVI configuration record", "dv_profile": 8 }
                  ]
                }
              ]
            }
            """;

        // DV only comes from video streams — an audio stream carrying this
        // (nonsense in practice) shouldn't trigger detection.
        ExtractDv(json).Should().BeNull();
    }

    [Fact]
    public void Parse_FullMediaInfo_ThreadsDvIntoResult()
    {
        const string json = """
            {
              "format": {
                "duration": "7200.000000",
                "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                "bit_rate": "15000000",
                "size": "13500000000"
              },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "width": 3840,
                  "height": 2160,
                  "pix_fmt": "yuv420p10le",
                  "r_frame_rate": "24/1",
                  "side_data_list": [
                    {
                      "side_data_type": "DOVI configuration record",
                      "dv_profile": 8,
                      "dv_level": 6,
                      "rpu_present_flag": 1,
                      "el_present_flag": 0,
                      "dv_bl_signal_compatibility_id": 1
                    }
                  ]
                }
              ]
            }
            """;

        MediaInfo media = MediaAnalyzer.ParseFfprobeJson(json, "/media/movie.mkv");

        media.DolbyVision.Should().NotBeNull();
        media.DolbyVision!.Profile.Should().Be(8);
        media.DolbyVision.BlCompat.Should().Be(DvBlCompatibility.Hdr10);
    }

    private static DolbyVisionInfo? ExtractDv(string json)
    {
        JArray streams = (JArray)JObject.Parse(json)["streams"]!;
        return MediaAnalyzer.ParseDolbyVision(streams);
    }
}
