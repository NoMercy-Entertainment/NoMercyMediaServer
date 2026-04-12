namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Profiles;

public record BatchEncodingRequest(
    string[] InputPaths,
    string OutputDirectory,
    EncodingProfile Profile,
    EncodingOptions? Options = null
);
