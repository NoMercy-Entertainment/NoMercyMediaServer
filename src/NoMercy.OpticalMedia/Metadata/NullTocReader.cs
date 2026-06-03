namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Stub <see cref="ITocReader"/> that always returns null.
/// Used until a native TOC reader is available for the target platform.
/// When null is returned, <see cref="AudioCdIdentifier"/> falls back to
/// NeedsManualAssignment.
/// </summary>
// TODO follow-up: native TOC read (Linux ioctl CDROMREADTOCENTRY, macOS IOKit)
public sealed class NullTocReader : ITocReader
{
    public Task<DiscToc?> ReadTocAsync(string drivePath, CancellationToken ct) =>
        Task.FromResult<DiscToc?>(null);
}
