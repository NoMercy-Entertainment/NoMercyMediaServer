namespace NoMercy.NmSystem.Configuration;

public class EncoderResourceConfig
{
    public double EncoderCpuHeadroomPercent { get; set; } = 90.0;
    public double EncoderGpuHeadroomPercent { get; set; } = 95.0;
    public long EncoderMinFreeMemoryMb { get; set; } = 1024;
}
