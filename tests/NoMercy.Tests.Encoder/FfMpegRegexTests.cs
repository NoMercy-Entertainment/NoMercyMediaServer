using NoMercy.Encoder;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class FfMpegRegexTests
{
    [Theory]
    [InlineData("frame=  120\nfps=30.00\nstream_0_0_q=28.0\nbitrate= 500.0kbits/s\ntotal_size=1234567\nout_time_us=5000000\nout_time_ms=5000000\nout_time=00:00:05.000000\ndup_frames=0\ndrop_frames=0\nspeed=2.00x\nprogress=continue\n")]
    public void ParseOutputData_ValidProgressBlock_ReturnsCorrectData(string output)
    {
        TimeSpan totalDuration = TimeSpan.FromSeconds(60);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(5.0, result!.CurrentTime.TotalSeconds, 1);
        Assert.InRange(result.ProgressPercentage, 8.0, 9.0); // ~8.33%
        Assert.Equal(2.0, result.Speed, 1);
        Assert.Equal(30.0, result.Fps, 1);
        Assert.Equal(120, result.Frame);
        Assert.Equal("500.0kbits/s", result.Bitrate);
    }

    [Fact]
    public void ParseOutputData_MidwayProgress_CalculatesRemainingCorrectly()
    {
        string output = "frame=  600\nfps=24.00\nbitrate= 1200.0kbits/s\nout_time=00:01:00.000000\nspeed=1.50x\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromMinutes(2);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(60.0, result!.CurrentTime.TotalSeconds, 1);
        Assert.InRange(result.ProgressPercentage, 49.0, 51.0); // ~50%
        // remaining = (120 - 60) / 1.5 = 40
        Assert.Equal(40.0, result.Remaining, 1);
    }

    [Fact]
    public void ParseOutputData_ZeroSpeed_RemainingIsZero()
    {
        string output = "frame=  0\nfps=0.00\nbitrate=N/A\nout_time=00:00:00.000000\nspeed=N/A\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromMinutes(2);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Speed);
        Assert.Equal(0.0, result.Remaining);
    }

    [Fact]
    public void ParseOutputData_WindowsLineEndings_ParsesCorrectly()
    {
        string output = "frame=  300\r\nfps=30.00\r\nbitrate= 800.0kbits/s\r\nout_time=00:00:10.000000\r\nspeed=1.00x\r\nprogress=continue\r\n";
        TimeSpan totalDuration = TimeSpan.FromSeconds(60);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(10.0, result!.CurrentTime.TotalSeconds, 1);
        Assert.Equal(30.0, result.Fps, 1);
    }

    [Fact]
    public void ParseOutputData_MissingOutTime_ReturnsZeroProgress()
    {
        string output = "frame=  0\nfps=0.00\nbitrate=N/A\nspeed=N/A\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromMinutes(2);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.ProgressPercentage);
        Assert.Equal(TimeSpan.Zero, result.CurrentTime);
    }

    [Fact]
    public void ParseOutputData_LongDuration_ParsesCorrectly()
    {
        string output = "frame=  86400\nfps=24.00\nbitrate= 5000.0kbits/s\nout_time=01:30:00.000000\nspeed=3.50x\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromHours(2);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.Equal(90 * 60, result!.CurrentTime.TotalSeconds, 1); // 1h30m
        Assert.InRange(result.ProgressPercentage, 74.0, 76.0); // ~75%
        Assert.Equal(3.5, result.Speed, 1);
    }

    [Fact]
    public void ParseOutputData_EmptyOutput_ReturnsZeroValues()
    {
        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData("", TimeSpan.FromMinutes(1));

        // With empty output, no keys are parsed, so out_time is empty, no match -> progress 0
        Assert.NotNull(result);
        Assert.Equal(0.0, result!.ProgressPercentage);
    }
}
