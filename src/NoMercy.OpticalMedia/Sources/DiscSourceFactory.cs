using NoMercy.NmSystem.Dto;

namespace NoMercy.OpticalMedia.Sources;

/// <summary>
/// Picks the right <see cref="IDiscSource"/> for a given disc type.
/// Implementations are DI-registered; the factory walks the registered
/// set and returns the one whose <see cref="IDiscSource.Type"/> matches.
/// Returns <c>null</c> for <see cref="OpticalDiscType.None"/> or any
/// disc type without a registered implementation.
/// </summary>
public sealed class DiscSourceFactory(IEnumerable<IDiscSource> sources)
{
    private readonly Dictionary<OpticalDiscType, IDiscSource> _byType = sources.ToDictionary(s =>
        s.Type
    );

    public IDiscSource? CreateFor(OpticalDiscType type) =>
        _byType.TryGetValue(type, out IDiscSource? source) ? source : null;
}
