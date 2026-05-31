namespace NoMercy.Encoder.Profiles;

public record LadderTier(
    int Width,
    int Height,
    string Label,
    int? RecommendedBitrateH264Kbps,
    int? RecommendedBitrateHevcKbps,
    int? RecommendedBitrateAv1Kbps,
    int? RecommendedBitrateVp9Kbps = null
);
