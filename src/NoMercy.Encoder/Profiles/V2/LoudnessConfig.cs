namespace NoMercy.Encoder.Profiles.V2;

public record LoudnessConfig(
    LoudnessMode Mode,
    double? TargetLufs = null,
    double? TruePeakDb = null
);
