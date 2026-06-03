namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Reads the raw CD Table of Contents from a drive.
/// Returns null when the TOC cannot be read (no disc, unsupported platform,
/// or read error), causing identification to degrade to
/// <see cref="DiscIdentification.NeedsManualAssignment"/>.
/// </summary>
public interface ITocReader
{
    Task<DiscToc?> ReadTocAsync(string drivePath, CancellationToken ct);
}
