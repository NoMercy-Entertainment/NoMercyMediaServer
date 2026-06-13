using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Lets a layer above the encoder replace the configured profile based on the
/// analyzed source — the seam that wires <c>IEncoderPlugin.GetProfile</c> without
/// the encoder taking a dependency on the plugin system (which depends on the
/// encoder). Applied once, after Analyze and before Validate. When no
/// implementation is registered the encoder uses the configured profile unchanged.
/// </summary>
public interface IProfileOverride
{
    EncodingProfile Apply(EncodingProfile configured, MediaInfo media);
}
