using NoMercy.Encoder.DiscRipping;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Tests the real <see cref="DiscScanner.Parse"/> JSON pipeline against
/// sample ffprobe envelopes. The fake-backed tests in
/// <see cref="DiscScannerTests"/> cover the interface contract; these lock
/// in how the raw JSON gets mapped into <see cref="DiscInfo"/>.
/// </summary>
public class DiscScannerParseTests
{
    [Fact]
    public void Parse_BluRayJson_ProducesSingleMainTitle()
    {
        const string json = """
            {
              "format": {
                "duration": "8160.000000",
                "tags": { "title": "THE_MATRIX" }
              },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "width": 1920,
                  "height": 1080,
                  "pix_fmt": "yuv420p10le"
                },
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "truehd",
                  "channels": 8,
                  "sample_rate": "48000",
                  "tags": { "language": "eng" }
                },
                {
                  "index": 2,
                  "codec_type": "subtitle",
                  "codec_name": "hdmv_pgs_subtitle",
                  "tags": { "language": "eng" }
                }
              ],
              "chapters": [
                { "start_time": "0.000000", "end_time": "120.000000", "tags": { "title": "Intro" } },
                { "start_time": "120.000000", "end_time": "8160.000000", "tags": { "title": "Main" } }
              ]
            }
            """;

        DiscInfo info = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        info.Type.Should().Be(OpticalDiscType.BluRay);
        info.DiscLabel.Should().Be("THE_MATRIX");
        info.TotalDuration.Should().Be(TimeSpan.FromSeconds(8160));
        info.Titles.Should().HaveCount(1);

        DiscTitle title = info.Titles[0];
        title.IsMainFeature.Should().BeTrue();
        title.VideoStreams.Should().HaveCount(1);
        title.VideoStreams[0].Codec.Should().Be("hevc");
        title.VideoStreams[0].Width.Should().Be(1920);
        title.AudioStreams.Should().HaveCount(1);
        title.AudioStreams[0].Codec.Should().Be("truehd");
        title.AudioStreams[0].Channels.Should().Be(8);
        title.AudioStreams[0].SampleRate.Should().Be(48000);
        title.AudioStreams[0].Language.Should().Be("eng");
        title.Subtitles.Should().HaveCount(1);
        title.Subtitles[0].Codec.Should().Be("hdmv_pgs_subtitle");
        title.Chapters.Should().HaveCount(2);
        title.Chapters[0].Title.Should().Be("Intro");
    }

    [Fact]
    public void Parse_DvdJson_ClassifiesAsDvd()
    {
        const string json = """
            {
              "format": { "duration": "5700.000000" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "mpeg2video",
                  "width": 720,
                  "height": 480,
                  "pix_fmt": "yuv420p"
                }
              ]
            }
            """;

        DiscInfo info = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        info.Type.Should().Be(OpticalDiscType.Dvd);
        info.Titles.Should().HaveCount(1);
        info.Titles[0].VideoStreams[0].Codec.Should().Be("mpeg2video");
        info.Titles[0].VideoStreams[0].Width.Should().Be(720);
        info.Titles[0].Chapters.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyStreams_ProducesTitleWithZeroStreams()
    {
        const string json = """
            { "format": { "duration": "0.000000" } }
            """;

        DiscInfo info = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        info.Titles.Should().HaveCount(1);
        info.Titles[0].VideoStreams.Should().BeEmpty();
        info.Titles[0].AudioStreams.Should().BeEmpty();
        info.Titles[0].Subtitles.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingDuration_UsesZero()
    {
        const string json = """
            {
              "format": { "tags": { "title": "DISC" } },
              "streams": []
            }
            """;

        DiscInfo info = DiscScanner.Parse(json, OpticalDiscType.Dvd);

        info.TotalDuration.Should().Be(TimeSpan.Zero);
        info.DiscLabel.Should().Be("DISC");
    }

    [Fact]
    public void Parse_MultipleAudioStreams_AllCaptured()
    {
        const string json = """
            {
              "format": { "duration": "3600.000000" },
              "streams": [
                { "index": 0, "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080 },
                { "index": 1, "codec_type": "audio", "codec_name": "ac3", "channels": 6, "tags": { "language": "eng" } },
                { "index": 2, "codec_type": "audio", "codec_name": "dts", "channels": 6, "tags": { "language": "fre" } },
                { "index": 3, "codec_type": "audio", "codec_name": "aac", "channels": 2, "tags": { "language": "spa" } }
              ]
            }
            """;

        DiscInfo info = DiscScanner.Parse(json, OpticalDiscType.BluRay);

        info.Titles[0].AudioStreams.Should().HaveCount(3);
        info.Titles[0].AudioStreams.Select(a => a.Language).Should().Equal("eng", "fre", "spa");
    }
}
