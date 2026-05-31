namespace NoMercy.Encoder.Bundle;

public interface IBundleManifestWriter
{
    Task WriteAsync(string path, BundleManifest manifest, CancellationToken ct);

    Task<BundleManifest?> ReadAsync(string path, CancellationToken ct);

    Task<ReconcileReport> ReconcileAsync(
        string bundleDirectory,
        BundleManifest manifest,
        CancellationToken ct
    );
}

public record ReconcileReport(IReadOnlyList<string> ExtraFiles, IReadOnlyList<string> MissingFiles);
