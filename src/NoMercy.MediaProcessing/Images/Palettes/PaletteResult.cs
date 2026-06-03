namespace NoMercy.MediaProcessing.Images.Palettes;

public sealed record PaletteResult(string Json, bool Permanent)
{
    public static PaletteResult Success(string json) => new(json, false);

    public static PaletteResult NoImage() => new("{}", true);
}
