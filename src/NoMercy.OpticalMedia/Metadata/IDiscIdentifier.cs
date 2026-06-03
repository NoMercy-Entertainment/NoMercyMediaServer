using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// One identification strategy for a disc type. Implementations are
/// registered via DI and dispatched by
/// <see cref="DiscIdentificationService"/> based on
/// <see cref="CanHandle"/>.
/// </summary>
public interface IDiscIdentifier
{
    /// <summary>
    /// Returns true when this identifier can handle the given disc type.
    /// </summary>
    bool CanHandle(OpticalDiscType type);

    /// <summary>
    /// Identifies the disc and returns ranked candidates with confidence.
    /// </summary>
    Task<DiscIdentification> IdentifyAsync(DiscInfo disc, CancellationToken ct);
}
