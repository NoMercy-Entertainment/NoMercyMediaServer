namespace NoMercy.Encoder.V3.Hardware;

using NoMercy.Encoder.V3.Codecs;

public class HardwareCapabilities(IReadOnlyList<GpuDevice> Gpus, int CpuCores)
    : IHardwareCapabilities
{
    public IReadOnlyList<GpuDevice> Gpus { get; } = Gpus;
    public int CpuCores { get; } = CpuCores;
    public bool HasGpu => Gpus.Count > 0;

    public bool SupportsHardwareEncoding(VideoCodecType codec)
    {
        return Gpus.Any(g => g.SupportedCodecs.Contains(codec));
    }

    public GpuDevice? GetGpuForCodec(VideoCodecType codec)
    {
        return Gpus.FirstOrDefault(g => g.SupportedCodecs.Contains(codec));
    }
}
