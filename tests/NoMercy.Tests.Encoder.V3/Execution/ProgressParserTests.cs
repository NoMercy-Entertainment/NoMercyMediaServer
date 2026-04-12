namespace NoMercy.Tests.Encoder.V3.Execution;

using NoMercy.Encoder.V3.Execution;

public class ProgressParserTests
{
    [Fact]
    public void Parse_CompleteBlock_ReturnsSnapshot()
    {
        ProgressParser parser = new();

        parser.FeedLine("frame=1234").Should().BeNull();
        parser.FeedLine("fps=59.8").Should().BeNull();
        parser.FeedLine("bitrate=8234.5kbits/s").Should().BeNull();
        parser.FeedLine("total_size=12345678").Should().BeNull();
        parser.FeedLine("out_time_us=60000000").Should().BeNull();
        parser.FeedLine("speed=2.50x").Should().BeNull();

        FfmpegProgressSnapshot? snapshot = parser.FeedLine("progress=continue");

        snapshot.Should().NotBeNull();
        snapshot!.Frame.Should().Be(1234);
        snapshot.Fps.Should().BeApproximately(59.8, 0.01);
        snapshot.BitrateKbps.Should().BeApproximately(8234.5, 0.1);
        snapshot.TotalSizeBytes.Should().Be(12345678);
        snapshot.OutTime.Should().Be(TimeSpan.FromSeconds(60));
        snapshot.Speed.Should().BeApproximately(2.5, 0.01);
        snapshot.IsEnd.Should().BeFalse();
    }

    [Fact]
    public void Parse_EndProgress_IsEndTrue()
    {
        ProgressParser parser = new();
        parser.FeedLine("frame=100");
        parser.FeedLine("fps=30.0");
        parser.FeedLine("speed=1.0x");
        parser.FeedLine("out_time_us=5000000");

        FfmpegProgressSnapshot? snapshot = parser.FeedLine("progress=end");

        snapshot.Should().NotBeNull();
        snapshot!.IsEnd.Should().BeTrue();
    }

    [Fact]
    public void Parse_NaBitrate_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine("bitrate=N/A");
        parser.FeedLine("speed=N/A");

        FfmpegProgressSnapshot? snapshot = parser.FeedLine("progress=continue");

        snapshot.Should().NotBeNull();
        snapshot!.BitrateKbps.Should().BeNull();
        snapshot.Speed.Should().Be(0);
    }

    [Fact]
    public void Parse_EmptyLine_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine("").Should().BeNull();
        parser.FeedLine("   ").Should().BeNull();
    }

    [Fact]
    public void Parse_MalformedLine_ReturnsNull()
    {
        ProgressParser parser = new();
        parser.FeedLine("no equals sign here").Should().BeNull();
    }

    [Fact]
    public void Parse_MultipleBlocks_ReturnsMultipleSnapshots()
    {
        ProgressParser parser = new();

        parser.FeedLine("frame=100");
        parser.FeedLine("speed=2.0x");
        parser.FeedLine("out_time_us=5000000");
        FfmpegProgressSnapshot? first = parser.FeedLine("progress=continue");

        parser.FeedLine("frame=200");
        parser.FeedLine("speed=2.5x");
        parser.FeedLine("out_time_us=10000000");
        FfmpegProgressSnapshot? second = parser.FeedLine("progress=continue");

        first.Should().NotBeNull();
        first!.Frame.Should().Be(100);
        second.Should().NotBeNull();
        second!.Frame.Should().Be(200);
        second.Speed.Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public void Parse_SpeedFormats()
    {
        ProgressParser parser = new();

        parser.FeedLine("speed=0.5x");
        FfmpegProgressSnapshot? slow = parser.FeedLine("progress=continue");
        slow!.Speed.Should().BeApproximately(0.5, 0.01);

        parser.FeedLine("speed=10.2x");
        FfmpegProgressSnapshot? fast = parser.FeedLine("progress=continue");
        fast!.Speed.Should().BeApproximately(10.2, 0.1);
    }

    [Fact]
    public void Parse_OutTimeConvertsCorrectly()
    {
        ProgressParser parser = new();
        parser.FeedLine("out_time_us=7200000000"); // 7200 seconds = 2 hours
        FfmpegProgressSnapshot? snapshot = parser.FeedLine("progress=continue");

        snapshot!.OutTime.Should().Be(TimeSpan.FromHours(2));
    }
}
