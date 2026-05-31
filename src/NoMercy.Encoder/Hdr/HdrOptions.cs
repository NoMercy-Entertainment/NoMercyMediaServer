namespace NoMercy.Encoder.Hdr;

public record HdrOptions(
    TonemapAlgorithm Algorithm = TonemapAlgorithm.Hable,
    string? CustomLutPath = null,
    LutApplication LutApply = LutApplication.AfterTonemap,
    double Desat = 0.0,
    double Peak = 0.0,
    bool PreserveMetadata = false
);

public enum TonemapAlgorithm
{
    Hable,
    Reinhard,
    Mobius,
    Bt2390,
}

public enum LutApplication
{
    BeforeTonemap,
    AfterTonemap,
    InsteadOfTonemap,
}
