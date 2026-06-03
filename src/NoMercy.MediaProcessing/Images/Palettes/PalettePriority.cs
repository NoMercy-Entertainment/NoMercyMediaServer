namespace NoMercy.MediaProcessing.Images.Palettes;

public static class PalettePriority
{
    public const int OnDemand = 1;
    public const int BackfillCoordinator = 20;

    private static readonly HashSet<string> MainTypes = new()
    {
        "movie",
        "tv",
        "season",
        "episode",
        "person",
        "collection",
        "artist",
        "album",
    };

    public static bool IsMain(string entityType) => MainTypes.Contains(entityType);

    public static int ForImport(string entityType) => IsMain(entityType) ? 4 : 8;

    public static int ForBackfill(string entityType) => IsMain(entityType) ? 14 : 18;
}
