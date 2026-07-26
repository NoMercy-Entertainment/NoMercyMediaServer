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
/// Matroska does not write <c>bit_rate</c> onto its streams, so a parser that
/// reads only that field reports 0 kbps for most of a real library. Downstream
/// nothing treats 0 as "unknown": the ABR ladder drops its never-upsource and
/// source-percentage rules, a bandwidth-capped client is told every file fits,
/// and smart-copy has no source figure to weigh a re-encode against. These
/// pin the fallbacks that keep a real figure flowing.
/// </summary>
[Trait("Category", "Unit")]
public class StreamBitrateParsingTests
{
    /// <summary>
    /// The exact video-stream JSON ffprobe returns for an mkvmerge-muxed
    /// episode — no <c>bit_rate</c> field, the real figure only in the
    /// statistics tags.
    /// </summary>
    private const string MkvmergeVideoStream = """
        {
          "codec_name": "hevc",
          "width": 1920,
          "height": 1080,
          "pix_fmt": "yuv420p10le",
          "r_frame_rate": "24000/1001",
          "tags": {
            "language": "jpn",
            "title": "WEBrip by sam",
            "BPS": "17982697",
            "DURATION": "00:24:00.022000000",
            "NUMBER_OF_FRAMES": "34526",
            "NUMBER_OF_BYTES": "3236934988",
            "_STATISTICS_WRITING_APP": "mkvmerge v98.0.35 ('Chonks') 64-bit",
            "_STATISTICS_TAGS": "BPS DURATION NUMBER_OF_FRAMES NUMBER_OF_BYTES"
          }
        }
        """;

    [Fact]
    public void MkvStreamWithoutBitRateField_ReadsTheStatisticsTag()
    {
        JToken stream = JToken.Parse(MkvmergeVideoStream);

        long kbps = MediaAnalyzer.ParseStreamBitrateKbps(stream);

        kbps.Should()
            .Be(
                17982,
                "the BPS statistics tag carries the real figure when ffprobe reports no bit_rate"
            );
    }

    [Fact]
    public void DeclaredBitRateField_WinsOverTheStatisticsTag()
    {
        JObject stream = (JObject)JToken.Parse(MkvmergeVideoStream);
        stream["bit_rate"] = "8000000";

        long kbps = MediaAnalyzer.ParseStreamBitrateKbps(stream);

        kbps.Should().Be(8000, "the container's own field is the more direct statement");
    }

    [Fact]
    public void WithoutBps_DerivesFromByteCountOverDuration()
    {
        JObject stream = (JObject)JToken.Parse(MkvmergeVideoStream);
        ((JObject)stream["tags"]!).Remove("BPS");

        long kbps = MediaAnalyzer.ParseStreamBitrateKbps(stream);

        // 3_236_934_988 bytes * 8 / 1440.022 s / 1000
        kbps.Should().BeInRange(17970, 17995);
    }

    [Fact]
    public void LanguageSuffixedTags_AreStillFound()
    {
        JObject stream = (JObject)JToken.Parse(MkvmergeVideoStream);
        JObject tags = (JObject)stream["tags"]!;
        tags.Remove("BPS");
        tags["BPS-eng"] = "17982697";

        long kbps = MediaAnalyzer.ParseStreamBitrateKbps(stream);

        kbps.Should()
            .Be(17982, "mkvmerge suffixes the statistics tags when a track declares a language");
    }

    [Fact]
    public void NothingRecorded_StaysZero()
    {
        JToken stream = JToken.Parse("""{ "codec_name": "hevc", "width": 1920, "height": 1080 }""");

        long kbps = MediaAnalyzer.ParseStreamBitrateKbps(stream);

        kbps.Should().Be(0, "a file that states no bitrate must not have one invented for it");
    }
}
