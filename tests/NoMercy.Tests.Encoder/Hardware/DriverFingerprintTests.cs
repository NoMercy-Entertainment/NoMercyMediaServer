namespace NoMercy.Tests.Encoder.Hardware;

using NoMercy.Encoder.Hardware;

public class DriverFingerprintTests
{
    private static GpuDriverInfo Nvidia(string driver = "31.0.15.4601") =>
        new("Nvidia", "RTX 4090", driver, 0);

    private static GpuDriverInfo Intel(string driver = "31.0.101.2134") =>
        new("Intel", "Arc A770", driver, 1);

    [Fact]
    public void ComputeHash_SameInputs_ProducesStableHash()
    {
        DriverFingerprint fp = new([Nvidia(), Intel()]);

        string first = fp.ComputeHash();
        string second = fp.ComputeHash();

        first.Should().Be(second);
    }

    [Fact]
    public void ComputeHash_OrderInsensitive_SameHashRegardlessOfGpuOrder()
    {
        DriverFingerprint fp1 = new([Nvidia(), Intel()]);
        DriverFingerprint fp2 = new([Intel(), Nvidia()]);

        fp1.ComputeHash().Should().Be(fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_ChangesWhenDriverVersionChanges()
    {
        DriverFingerprint fp1 = new([Nvidia("31.0.15.4601")]);
        DriverFingerprint fp2 = new([Nvidia("31.0.15.5000")]);

        fp1.ComputeHash().Should().NotBe(fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_ChangesWhenGpuAdded()
    {
        DriverFingerprint fp1 = new([Nvidia()]);
        DriverFingerprint fp2 = new([Nvidia(), Intel()]);

        fp1.ComputeHash().Should().NotBe(fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_HandlesEmptyDriverVersion_ProducesStableHash()
    {
        DriverFingerprint fp1 = new([new GpuDriverInfo("Nvidia", "RTX 4090", string.Empty, 0)]);
        DriverFingerprint fp2 = new([new GpuDriverInfo("Nvidia", "RTX 4090", string.Empty, 0)]);

        string hash = fp1.ComputeHash();

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Be(fp2.ComputeHash());
    }
}
