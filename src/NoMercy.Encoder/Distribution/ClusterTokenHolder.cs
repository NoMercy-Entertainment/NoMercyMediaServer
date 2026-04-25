namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Singleton holder that carries the latest <see cref="ClusterToken"/>
/// issued by the license coordinator and makes the derived
/// <see cref="HmacSigner"/> available without any DI cycles.
///
/// Pattern mirrors <see cref="NoMercy.Encoder.Hardware.HardwareCapabilitiesHolder"/>:
/// the background service writes here after every successful token rotation;
/// <see cref="NoMercy.Api.Middleware.HmacValidationMiddleware"/> reads
/// <see cref="GetSigner"/> for the current signing key.
///
/// Thread-safety: a volatile reference swap is safe for reads; the
/// background service is the only writer and it holds no lock on the write
/// path — the old signer is simply GC'd after the reference is replaced.
/// </summary>
public sealed class ClusterTokenHolder
{
    private volatile ClusterToken? _current;

    /// <summary>
    /// The most recently issued cluster token, or null when none has been
    /// received yet (free-tier install, or before first coordinator contact).
    /// </summary>
    public ClusterToken? Current => _current;

    /// <summary>
    /// True when a non-expired token is held. Used by the dashboard health
    /// endpoint to surface "license_revoked" state.
    /// </summary>
    public bool HasValidToken => _current is not null && _current.ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// True when the license has been explicitly revoked (set by
    /// <see cref="WorkerSelfRegistrationService"/> on a 403 response).
    /// Resets on service restart — a revoked license requires the operator
    /// to take corrective action and restart.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Replaces the current token and builds a fresh
    /// <see cref="HmacSigner"/> backed by the new secret.
    /// Called by the background service on every successful rotation.
    /// </summary>
    public void Update(ClusterToken token)
    {
        _current = token;
        _activeSigner = new HmacSigner(token.Secret);
    }

    private volatile HmacSigner? _activeSigner;

    /// <summary>
    /// Returns the HMAC signer backed by the current cluster-token secret,
    /// or null when no valid token is held. Callers must fall back to the
    /// static <see cref="EncoderOptions.DistributedEncodingSigningKey"/>
    /// when this returns null.
    /// </summary>
    public HmacSigner? GetSigner() => HasValidToken ? _activeSigner : null;
}
