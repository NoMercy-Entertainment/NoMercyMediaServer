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

using System.Globalization;
using NoMercy.Encoder.Analysis;

namespace NoMercy.Tests.Encoder.Analysis;

/// <summary>
/// Pins <see cref="MediaAnalyzer"/>'s r_frame_rate / avg_frame_rate parsing to
/// InvariantCulture. A tool upstream of ffprobe (or a malformed remux) can emit
/// a grouped-looking numerator like "24.000/1" instead of a clean integer
/// fraction; on a comma-decimal server locale (nl-NL/de-DE) a bare
/// double.TryParse treats "." as the current culture's thousands-group
/// separator and strips it, turning 24 fps into 24000 fps.
/// </summary>
public class MediaAnalyzerCultureTests
{
    private const string MalformedFrameRateFixture = """
        {
          "streams": [
            {
              "index": 0,
              "codec_name": "h264",
              "codec_type": "video",
              "width": 1920,
              "height": 1080,
              "r_frame_rate": "24.000/1",
              "avg_frame_rate": "24.000/1",
              "pix_fmt": "yuv420p"
            }
          ],
          "chapters": [],
          "format": {
            "format_name": "matroska,webm",
            "duration": "7200.000000",
            "bit_rate": "8192000",
            "size": "7372800000"
          }
        }
        """;

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void ParseFrameRate_GroupedLookingNumerator_StaysInvariantUnderCommaCulture(
        string culture
    )
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            MediaInfo info = MediaAnalyzer.ParseFfprobeJson(
                json: MalformedFrameRateFixture,
                filePath: "/media/test.mkv"
            );
            VideoStreamInfo video = info.VideoStreams[index: 0];

            // Old code (bare double.TryParse, CurrentCulture + AllowThousands)
            // strips the "." and reads "24.000" as 24000 on these locales.
            video.FrameRate.Should().Be(expected: 24.0);
            video.AverageFrameRate.Should().Be(expected: 24.0);
            video.RealFrameRate.Should().Be(expected: 24.0);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
