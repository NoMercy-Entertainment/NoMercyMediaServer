namespace NoMercy.Encoder.BuildingBlocks.Drm;

/// <summary>
/// Which DRM scheme to apply. Today only <see cref="Aes128"/> is implemented —
/// it's the baseline for HLS and covers the vast majority of self-hosted
/// deployments. CENC (Widevine / PlayReady / FairPlay) for DASH lands as a
/// follow-up and requires <c>shaka-packager</c> or similar.
/// </summary>
public enum DrmMethod
{
    None,
    Aes128,
}

/// <summary>
/// Per-profile DRM configuration. <see cref="KeyUri"/> is the URL HLS clients
/// hit to fetch the decryption key — the server is responsible for returning
/// the raw 16-byte key to authenticated players.
///
/// The encoder never owns key generation policy: it just uses the key it's
/// given (or generates a fresh random one if none provided) and tells the
/// player where to fetch it. Licensing / rotation is a server-level concern.
/// </summary>
public record DrmConfig(DrmMethod Method, string KeyUri, byte[]? Key = null, byte[]? Iv = null);
