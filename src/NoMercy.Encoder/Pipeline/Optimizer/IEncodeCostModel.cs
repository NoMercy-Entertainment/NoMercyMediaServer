using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>Output pixels + encoder speed → relative (unitless) encode cost.</summary>
public record CostRung(int Width, int Height, VideoCodecType Codec, string Encoder, int Passes);

public interface IEncodeCostModel
{
    double RungCost(int width, int height, VideoCodecType codec, string encoder, int passes);
    double TotalCost(int sourceWidth, int sourceHeight, bool sourceIsHdr, CostRung[] rungs);
}
