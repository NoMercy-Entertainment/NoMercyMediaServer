namespace NoMercy.Encoder.Profiles;

public record DrmConfig(string Scheme, Dictionary<string, string>? Parameters = null);
