namespace NoMercy.Encoder.Profiles.V2;

public record DrmConfig(string Scheme, Dictionary<string, string>? Parameters = null);
