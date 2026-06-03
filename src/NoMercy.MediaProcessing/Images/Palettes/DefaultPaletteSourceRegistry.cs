using NoMercy.MediaProcessing.Images.Palettes.Sources;

namespace NoMercy.MediaProcessing.Images.Palettes;

/// <summary>
/// Provides the default <see cref="PaletteSourceRegistry"/> used by queue jobs
/// that cannot receive DI-injected registries. All sources are stateless and safe
/// to instantiate without a container.
/// </summary>
public static class DefaultPaletteSourceRegistry
{
    private static readonly PaletteSourceRegistry _instance = new([
        new MoviePaletteSource(),
        new TvPaletteSource(),
        new SeasonPaletteSource(),
        new EpisodePaletteSource(),
        new CollectionPaletteSource(),
        new PersonPaletteSource(),
        new RecommendationPaletteSource(),
        new SimilarPaletteSource(),
        new ImagePaletteSource(),
        new ArtistPaletteSource(),
        new AlbumPaletteSource(),
        new PlaylistPaletteSource(),
        new ReleaseGroupPaletteSource(),
    ]);

    public static PaletteSourceRegistry Instance => _instance;
}
