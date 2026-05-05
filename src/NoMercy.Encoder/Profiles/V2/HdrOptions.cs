namespace NoMercy.Encoder.Profiles.V2;

public record HdrOptions(string Algorithm = "hable", int? PeakNits = null, string? LutPath = null);
