namespace NoMercy.Helpers.Wallpaper;

public class NullWallpaperService : IWallpaperService
{
    public bool IsSupported => false;
    public void Set(string imagePath, WallpaperStyle style, string hexColor) { }
    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor) { }
    public void Restore() { }
}
