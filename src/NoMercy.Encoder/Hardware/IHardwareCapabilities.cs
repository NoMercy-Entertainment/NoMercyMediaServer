using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Hardware;

public interface IHardwareCapabilities
{
    IReadOnlyList<GpuDevice> Gpus { get; }
    int CpuCores { get; }
    bool HasGpu { get; }
    bool SupportsHardwareEncoding(VideoCodecType codec);
    GpuDevice? GetGpuForCodec(VideoCodecType codec);
}
