namespace NoMercy.Encoder.V3.Hardware;

using NoMercy.Encoder.V3.Codecs;

public interface IHardwareCapabilities
{
    IReadOnlyList<GpuDevice> Gpus { get; }
    int CpuCores { get; }
    bool HasGpu { get; }
    bool SupportsHardwareEncoding(VideoCodecType codec);
    GpuDevice? GetGpuForCodec(VideoCodecType codec);
}
