namespace NoMercy.Helpers.Wallpaper;

public interface IWallpaperService
{
    bool IsSupported { get; }
    void Set(string imagePath, WallpaperStyle style, string hexColor);
    void SetSilent(string imagePath, WallpaperStyle style, string hexColor);
    void Restore();
}
