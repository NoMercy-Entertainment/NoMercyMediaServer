namespace NoMercy.Encoder.Profiles.V2;

public record LadderTier(
    int Width,
    int Height,
    string Label,
    int? RecommendedBitrateH264Kbps,
    int? RecommendedBitrateHevcKbps,
    int? RecommendedBitrateAv1Kbps
);
