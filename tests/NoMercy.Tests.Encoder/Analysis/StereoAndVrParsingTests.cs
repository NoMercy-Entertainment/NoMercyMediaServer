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
/// Exercises ParseStereoMode and ParseSphericalProjection from MediaAnalyzer,
/// covering MKV tag path and MP4 side_data_list path for each.
/// </summary>
public class StereoAndVrParsingTests
{
    // ------------------------------------------------------------------
    // ParseStereoMode — MKV tag path
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_DetectsStereoMode_MkvTag()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "tags": {
                    "stereo_mode": "side_by_side_left"
                  }
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        string? result = MediaAnalyzer.ParseStereoMode(streams: streams);

        result.Should().Be(expected: "side_by_side_left");
    }

    // ------------------------------------------------------------------
    // ParseStereoMode — MP4 side_data_list (Stereo 3D box)
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_DetectsStereoMode_Mp4SideData()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "h264",
                  "side_data_list": [
                    {
                      "side_data_type": "Stereo 3D",
                      "type": "top_bottom_left"
                    }
                  ]
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        string? result = MediaAnalyzer.ParseStereoMode(streams: streams);

        result.Should().Be(expected: "top_bottom_left");
    }

    // ------------------------------------------------------------------
    // ParseStereoMode — audio stream ignored
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_StereoMode_IgnoresAudioStreams()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "audio",
                  "tags": { "stereo_mode": "side_by_side_left" }
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        MediaAnalyzer.ParseStereoMode(streams: streams).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // ParseStereoMode — no 3D data
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_StereoMode_NullWhenNoData()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264" }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        MediaAnalyzer.ParseStereoMode(streams: streams).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // ParseSphericalProjection — MP4 sv3d side_data_list
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_DetectsSphericalProjection_Mp4SideData()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "codec_name": "h264",
                  "side_data_list": [
                    {
                      "side_data_type": "Spherical Mapping",
                      "projection": "equirectangular"
                    }
                  ]
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        string? result = MediaAnalyzer.ParseSphericalProjection(streams: streams);

        result.Should().Be(expected: "equirectangular");
    }

    // ------------------------------------------------------------------
    // ParseSphericalProjection — cubemap variant
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_DetectsSphericalProjection_Cubemap()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "video",
                  "side_data_list": [
                    {
                      "side_data_type": "Spherical Mapping",
                      "projection": "cubemap"
                    }
                  ]
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        MediaAnalyzer.ParseSphericalProjection(streams: streams).Should().Be(expected: "cubemap");
    }

    // ------------------------------------------------------------------
    // ParseSphericalProjection — audio stream ignored
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_SphericalProjection_IgnoresAudioStreams()
    {
        const string json = """
            {
              "streams": [
                {
                  "codec_type": "audio",
                  "side_data_list": [
                    {
                      "side_data_type": "Spherical Mapping",
                      "projection": "equirectangular"
                    }
                  ]
                }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        MediaAnalyzer.ParseSphericalProjection(streams: streams).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // ParseSphericalProjection — null when no VR data
    // ------------------------------------------------------------------

    [Fact]
    public void MediaAnalyzer_SphericalProjection_NullWhenNoData()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264" }
              ]
            }
            """;

        JArray streams = (JArray)JObject.Parse(json: json)[propertyName: "streams"]!;
        MediaAnalyzer.ParseSphericalProjection(streams: streams).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // ParseFfprobeJson integration — StereoMode threads into MediaInfo
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFfprobeJson_StereoMode_SetsFieldOnMediaInfo()
    {
        const string json = """
            {
              "format": {
                "duration": "3600",
                "format_name": "matroska,webm",
                "bit_rate": "8000000",
                "size": "3600000000"
              },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080,
                  "r_frame_rate": "24/1",
                  "pix_fmt": "yuv420p",
                  "tags": { "stereo_mode": "side_by_side_left" },
                  "disposition": { "default": 1 }
                }
              ]
            }
            """;

        MediaInfo media = MediaAnalyzer.ParseFfprobeJson(json: json, filePath: "/media/3d.mkv");

        media.StereoMode.Should().Be(expected: "side_by_side_left");
        media.SphericalProjection.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // ParseFfprobeJson integration — SphericalProjection threads into MediaInfo
    // ------------------------------------------------------------------

    [Fact]
    public void ParseFfprobeJson_SphericalProjection_SetsFieldOnMediaInfo()
    {
        const string json = """
            {
              "format": {
                "duration": "600",
                "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                "bit_rate": "20000000",
                "size": "1500000000"
              },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 3840,
                  "height": 1920,
                  "r_frame_rate": "30/1",
                  "pix_fmt": "yuv420p",
                  "side_data_list": [
                    {
                      "side_data_type": "Spherical Mapping",
                      "projection": "equirectangular"
                    }
                  ],
                  "disposition": { "default": 1 }
                }
              ]
            }
            """;

        MediaInfo media = MediaAnalyzer.ParseFfprobeJson(json: json, filePath: "/media/vr360.mp4");

        media.SphericalProjection.Should().Be(expected: "equirectangular");
        media.StereoMode.Should().BeNull();
    }
}
