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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait("Category", "Unit")]
public class DiscScannerTests
{
    private static DiscScanner MakeSut(IProcessRunner? runner = null)
    {
        EncoderOptions options = new() { FfprobePathOverride = "ffprobe" };
        IProcessRunner processRunner = runner ?? Moq.Mock.Of<IProcessRunner>();
        return new DiscScanner(options, processRunner, NullLogger<DiscScanner>.Instance);
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsSingleEmptyTitle()
    {
        string json = """{"format":{}}""";
        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Type.Should().Be(OpticalDiscType.BluRay);
        result.Titles.Should().HaveCount(1);
        result.Titles[0].Duration.Should().Be(TimeSpan.Zero);
        result.TotalDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Parse_WithValidTitle_ReturnsSingleTitleWithDuration()
    {
        string json = """
            {
              "format": {
                "tags": { "title": "Test Movie" },
                "duration": "7200.5"
              }
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        result.Titles.Should().HaveCount(1);
        result.Titles[0].Name.Should().Be("Test Movie");
        result
            .Titles[0]
            .Duration.Should()
            .BeCloseTo(TimeSpan.FromSeconds(7200.5), TimeSpan.FromMilliseconds(1));
        result.Type.Should().Be(OpticalDiscType.Dvd);
    }

    [Fact]
    public void Parse_WithVideoStreams_ParsesCodecAndResolution()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080,
                  "pix_fmt": "yuv420p"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().HaveCount(1);
        result.Titles[0].VideoStreams[0].Codec.Should().Be("h264");
        result.Titles[0].VideoStreams[0].Width.Should().Be(1920);
        result.Titles[0].VideoStreams[0].Height.Should().Be(1080);
        result.Titles[0].VideoStreams[0].PixelFormat.Should().Be("yuv420p");
    }

    [Fact]
    public void Parse_WithAudioStreams_ParsesCodecChannelsAndLanguage()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000",
                  "tags": { "language": "eng" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        result.Titles[0].AudioStreams.Should().HaveCount(1);
        result.Titles[0].AudioStreams[0].Codec.Should().Be("ac3");
        result.Titles[0].AudioStreams[0].Channels.Should().Be(6);
        result.Titles[0].AudioStreams[0].SampleRate.Should().Be(48000);
        result.Titles[0].AudioStreams[0].Language.Should().Be("eng");
    }

    [Fact]
    public void Parse_WithSubtitleStreams_ParsesCodecAndLanguage()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 2,
                  "codec_type": "subtitle",
                  "codec_name": "dvd_subtitle",
                  "tags": { "language": "eng" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        result.Titles[0].Subtitles.Should().HaveCount(1);
        result.Titles[0].Subtitles[0].Codec.Should().Be("dvd_subtitle");
        result.Titles[0].Subtitles[0].Language.Should().Be("eng");
    }

    [Fact]
    public void Parse_WithMultipleAudioTracks_AllParsed()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000",
                  "tags": { "language": "eng" }
                },
                {
                  "index": 2,
                  "codec_type": "audio",
                  "codec_name": "aac",
                  "channels": 2,
                  "sample_rate": "48000",
                  "tags": { "language": "fra" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].AudioStreams.Should().HaveCount(2);
        result.Titles[0].AudioStreams[0].Language.Should().Be("eng");
        result.Titles[0].AudioStreams[1].Language.Should().Be("fra");
    }

    [Fact]
    public void Parse_WithChapters_ParsesStartEndAndTitle()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "chapters": [
                {
                  "start_time": "0.0",
                  "end_time": "600.0",
                  "tags": { "title": "Prologue" }
                },
                {
                  "start_time": "600.0",
                  "end_time": "3600.0",
                  "tags": { "title": "Main Feature" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        result.Titles[0].Chapters.Should().HaveCount(2);
        result.Titles[0].Chapters[0].Start.Should().Be(TimeSpan.Zero);
        result.Titles[0].Chapters[0].Title.Should().Be("Prologue");
        result.Titles[0].Chapters[1].Start.Should().Be(TimeSpan.FromSeconds(600));
        result.Titles[0].Chapters[1].Title.Should().Be("Main Feature");
    }

    [Fact]
    public void Parse_WithoutLanguageTags_AudioStreamLanguageIsNull()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].AudioStreams[0].Language.Should().BeNull();
    }

    [Fact]
    public void Parse_WithoutChapterTags_ChapterTitleIsNull()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "chapters": [
                {
                  "start_time": "0.0",
                  "end_time": "600.0"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        result.Titles[0].Chapters[0].Title.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJsonThrows()
    {
        string json = """{ invalid json }""";

        Action act = () => DiscScanner.Parse(json, OpticalDiscType.BluRay);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ffprobe output was not valid JSON*");
    }

    [Fact]
    public void Parse_MalformedDurationString_ParsesAsZero()
    {
        string json = """
            {
              "format": { "duration": "not_a_number" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].Duration.Should().Be(TimeSpan.Zero);
        result.Titles[0].VideoStreams[0].Width.Should().Be(1920);
        result.Titles[0].VideoStreams[0].Height.Should().Be(1080);
    }

    [Fact]
    public void Parse_UnknownCodecTypeIsIgnored()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "data"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().BeEmpty();
        result.Titles[0].AudioStreams.Should().BeEmpty();
        result.Titles[0].Subtitles.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SetsIsMainFeatureToTrue()
    {
        string json = """{"format": {"duration": "3600"}}""";

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].IsMainFeature.Should().BeTrue();
    }

    [Fact]
    public void Parse_SetsIndexToZero()
    {
        string json = """{"format": {"duration": "3600"}}""";

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].Index.Should().Be(0);
    }

    [Theory]
    [InlineData(OpticalDiscType.BluRay)]
    [InlineData(OpticalDiscType.Dvd)]
    [InlineData(OpticalDiscType.Cd)]
    public void Parse_DiscTypePassedThrough(OpticalDiscType type)
    {
        string json = """{"format":{}}""";

        DiscInfo result = DiscScanner.Parse(json, type);

        result.Type.Should().Be(type);
    }

    [Fact]
    public void Parse_EstimatedSizeSetToZero()
    {
        string json = """{"format":{"duration":"3600"}}""";

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].EstimatedSizeBytes.Should().Be(0);
    }

    [Fact]
    public void Parse_MultipleVideoStreamsAllParsed()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080
                },
                {
                  "index": 1,
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "width": 3840,
                  "height": 2160
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().HaveCount(2);
        result.Titles[0].VideoStreams[0].Codec.Should().Be("h264");
        result.Titles[0].VideoStreams[1].Codec.Should().Be("hevc");
    }
}
