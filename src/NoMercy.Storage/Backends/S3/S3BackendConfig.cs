namespace NoMercy.Storage.Backends.S3;

/// <summary>
/// Parsed representation of the JSON <c>BackendConfig</c> for S3-compatible
/// folder backends (AWS S3, Cloudflare R2, MinIO, DigitalOcean Spaces, …).
/// </summary>
internal sealed record S3BackendConfig(
    string Bucket,
    string Region,
    string? Prefix,
    string? CredentialsRef,
    string? Endpoint
);
