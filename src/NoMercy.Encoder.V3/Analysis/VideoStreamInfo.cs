namespace NoMercy.Encoder.V3.Analysis;

public record VideoStreamInfo(
    int Index,
    string Codec,
    int Width,
    int Height,
    double FrameRate,
    int BitDepth,
    string PixelFormat,
    string? ColorPrimaries,
    string? ColorTransfer,
    string? ColorSpace,
    bool IsDefault,
    long BitRateKbps,
    double? AverageFrameRate = null,
    double? RealFrameRate = null
)
{
    private static readonly HashSet<string> HdrTransfers = ["smpte2084", "arib-std-b67"];

    public bool IsHdr =>
        ColorTransfer is not null
        && HdrTransfers.Contains(ColorTransfer)
        && ColorPrimaries is "bt2020";

    public bool IsVariableFrameRate =>
        AverageFrameRate.HasValue
        && RealFrameRate.HasValue
        && Math.Abs(AverageFrameRate.Value - RealFrameRate.Value) > 1.0;
}
