namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for S3-compatible
/// folder drivers (AWS S3, Cloudflare R2, MinIO, DigitalOcean Spaces, …).
/// </summary>
internal sealed record S3DriverConfig(
    string Bucket,
    string Region,
    string? Prefix,
    string? CredentialsRef,
    string? Endpoint
);
