namespace NoMercy.MediaProcessing.Images.Palettes;

public class PaletteSourceRegistry
{
    private readonly Dictionary<string, IPaletteSource> _sources;

    public PaletteSourceRegistry(IEnumerable<IPaletteSource> sources)
    {
        _sources = sources.ToDictionary(s => s.EntityType, s => s);
    }

    public IPaletteSource? Resolve(string entityType) => _sources.GetValueOrDefault(entityType);

    public IReadOnlyCollection<string> EntityTypes => _sources.Keys.ToList();
}
