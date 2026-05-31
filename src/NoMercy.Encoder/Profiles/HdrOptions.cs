namespace NoMercy.Encoder.Profiles;

public record HdrOptions(string Algorithm = "hable", int? PeakNits = null, string? LutPath = null);
