namespace NoMercy.Encoder.Hardware;

using NoMercy.Encoder.Codecs;

public interface IHardwareCapabilities
{
    IReadOnlyList<GpuDevice> Gpus { get; }
    int CpuCores { get; }
    bool HasGpu { get; }
    bool SupportsHardwareEncoding(VideoCodecType codec);
    GpuDevice? GetGpuForCodec(VideoCodecType codec);
}
