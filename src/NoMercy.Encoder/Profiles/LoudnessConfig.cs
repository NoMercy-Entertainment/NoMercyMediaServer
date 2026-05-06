namespace NoMercy.Encoder.Profiles;

public record LoudnessConfig(
    LoudnessMode Mode,
    double? TargetLufs = null,
    double? TruePeakDb = null
);
