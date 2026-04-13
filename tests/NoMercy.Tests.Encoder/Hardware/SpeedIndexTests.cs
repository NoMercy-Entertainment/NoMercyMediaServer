namespace NoMercy.Tests.Encoder.Hardware;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

public class SpeedIndexTests
{
    [Fact]
    public void GetSpeed_ExistingKey_ReturnsMeasurement()
    {
        SpeedKey key = new(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4090");
        SpeedMeasurement measurement = new(120.0, 5.0, DateTime.UtcNow);
        SpeedIndex index = new(new Dictionary<SpeedKey, SpeedMeasurement> { [key] = measurement });

        SpeedMeasurement? result = index.GetSpeed(
            VideoCodecType.H264,
            "h264_nvenc",
            1920,
            "RTX 4090"
        );

        result.Should().NotBeNull();
        result!.Fps.Should().Be(120.0);
        result.SpeedMultiplier.Should().Be(5.0);
    }

    [Fact]
    public void GetSpeed_NonExistentKey_ReturnsNull()
    {
        SpeedIndex index = new(new Dictionary<SpeedKey, SpeedMeasurement>());
        SpeedMeasurement? result = index.GetSpeed(
            VideoCodecType.H264,
            "h264_nvenc",
            1920,
            "RTX 4090"
        );
        result.Should().BeNull();
    }

    [Fact]
    public void GetSpeedMultiplier_ExistingKey_ReturnsValue()
    {
        SpeedKey key = new(VideoCodecType.H265, "hevc_nvenc", 1080, "RTX 4090");
        SpeedMeasurement measurement = new(60.0, 2.5, DateTime.UtcNow);
        SpeedIndex index = new(new Dictionary<SpeedKey, SpeedMeasurement> { [key] = measurement });

        double mult = index.GetSpeedMultiplier(VideoCodecType.H265, "hevc_nvenc", 1080, "RTX 4090");
        mult.Should().Be(2.5);
    }

    [Fact]
    public void GetSpeedMultiplier_NonExistentKey_ReturnsZero()
    {
        SpeedIndex index = new(new Dictionary<SpeedKey, SpeedMeasurement>());
        double mult = index.GetSpeedMultiplier(VideoCodecType.H265, "hevc_nvenc", 1080, null);
        mult.Should().Be(0);
    }

    [Fact]
    public void SpeedKey_DifferentDevices_AreDifferentKeys()
    {
        SpeedKey key1 = new(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4090");
        SpeedKey key2 = new(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 3080");
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void SpeedKey_SameValues_AreEqual()
    {
        SpeedKey key1 = new(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4090");
        SpeedKey key2 = new(VideoCodecType.H264, "h264_nvenc", 1920, "RTX 4090");
        key1.Should().Be(key2);
    }

    [Fact]
    public void SpeedKey_NullDevice_MatchesNullDevice()
    {
        SpeedKey key1 = new(VideoCodecType.Av1, "libsvtav1", 1920, null);
        SpeedKey key2 = new(VideoCodecType.Av1, "libsvtav1", 1920, null);
        key1.Should().Be(key2);
    }
}
