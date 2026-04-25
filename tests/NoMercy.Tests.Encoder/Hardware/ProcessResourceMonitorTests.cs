namespace NoMercy.Tests.Encoder.Hardware;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

public class ProcessResourceMonitorTests
{
    [Fact]
    public void GetCpuUsagePercent_FirstCall_ReturnsNonNegativeValue()
    {
        ProcessResourceMonitor sut = new();

        double percent = sut.GetCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetCpuUsagePercent_SecondCall_ReturnsRelativeValue()
    {
        ProcessResourceMonitor sut = new();
        // Prime the snapshot cache.
        sut.GetCpuUsagePercent();
        Thread.Sleep(10);

        double percent = sut.GetCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetAvailableMemoryMb_ReturnsNonNegative()
    {
        ProcessResourceMonitor sut = new();

        long mb = sut.GetAvailableMemoryMb();

        mb.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetGpuEncodeUtilization_IsZero_WithoutVendorPlugin()
    {
        ProcessResourceMonitor sut = new();
        GpuDevice nvidia = new(
            Vendor: GpuVendor.Nvidia,
            Name: "RTX 4080",
            VramMb: 16_384,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264]
        );

        double util = sut.GetGpuEncodeUtilization(nvidia);

        util.Should().Be(0.0);
    }

    [Fact]
    public void NullResourceMonitor_ReturnsZeros()
    {
        NullResourceMonitor sut = new();

        sut.GetCpuUsagePercent().Should().Be(0);
        sut.GetAvailableMemoryMb().Should().Be(0);
        sut.GetGpuEncodeUtilization(
                new(GpuVendor.Nvidia, "n/a", VramMb: 0, MaxEncoderSessions: 0, SupportedCodecs: [])
            )
            .Should()
            .Be(0);
    }
}
