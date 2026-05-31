namespace NoMercy.Service.Seeds.Data;

public static class EncoderProfileSeedData
{
    public record SeedExample(string Name, string ParentBuiltinName);

    public static readonly SeedExample[] Examples =
    [
        new("Example: Web 1080p", "Web 1080p Balanced"),
        new("Example: Anime 1080p", "Anime HEVC 1080p 10-bit"),
        new("Example: Music FLAC", "Music FLAC Lossless"),
    ];
}
