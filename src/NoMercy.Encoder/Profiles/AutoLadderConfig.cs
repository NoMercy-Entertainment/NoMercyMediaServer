using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public record AutoLadderConfig
{
    public LadderTier[] Tiers { get; init; } = LadderTiers.AppleHlsRecommended;
    public BitrateStrategy BitrateStrategy { get; init; } = BitrateStrategy.AppleHlsRecommended;
    public int Crf { get; init; } = 22;
    public double SourcePercentage { get; init; } = 50.0;

    // Default 10 (was 5) so the YouTube ladder's 8 tiers (144p..2160p) survive
    // when JSON deserialization races init-only setters against the C# default.
    // Smaller ladders (Standard 3-rung, Premium 4-rung) are unaffected.
    public int MaxRungs { get; init; } = 10;
    public int MinRungs { get; init; } = 1;
    public bool NeverUpscale { get; init; } = true;
    public bool NeverUpsource { get; init; } = true;
    public double MinTierGapPercent { get; init; } = 50.0;
    public LadderCodecPolicy CodecPolicy { get; init; } = LadderCodecPolicy.Uniform;
    public VideoCodecType? LowTierCodec { get; init; }
    public VideoCodecType? HighTierCodec { get; init; }
    public int MixedPolicySplitHeight { get; init; } = 720;
    public double VbrCeilingMultiplier { get; init; } = 1.5;
    public double BufferSizeMultiplier { get; init; } = 2.0;
    public bool ReduceFramerateForLowTiers { get; init; }
    public double LowTierFramerateMultiplier { get; init; } = 0.5;
    public int LowTierFramerateThresholdHeight { get; init; } = 480;
}
