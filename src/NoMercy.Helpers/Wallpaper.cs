using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

namespace NoMercy.Helpers;
[SupportedOSPlatform("windows10.0.18362")]
public static class Wallpaper
{
    private const string DesktopRegPath = @"Control Panel\Desktop";
    private const string DesktopRegColor = @"Control Panel\Colors";
    private const string HistoryRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
    private const string WallpaperStyleRegPath = "WallpaperStyle";
    private const string WallpaperStyleRegColor = "WallpaperColor";
    private const string TileWallpaperRegPath = "TileWallpaper";

    private const int HistoryMaxEntries = 5;

    private const int ColorDesktop = 1;
    private const int SpiSetdeskwallpaper = 20;
    private const int SpifUpdateinifile = 0x01;
    private const int SpifSendwininichange = 0x02;

    [DllImport("user32.dll")]
    public static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);

    private static State? _backupState;
    private static bool _historyRestored;

    private struct Config
    {
        public int Style;
        public bool IsTile;
        public string Color;
    }

    private struct State
    {
        public Config Config;
        public string?[] History;
        public string Wallpaper;
        public string Color;
    }

    private static int GetRegistryValue(RegistryKey key, string name, int defaultValue)
    {
        return int.Parse((string)key.GetValue(name) ?? defaultValue.ToString());
    }

    private static bool GetRegistryValue(RegistryKey key, string name, bool defaultValue)
    {
        return ((string)key.GetValue(name) ?? (defaultValue ? "1" : "0")) == "1";
    }

    private static string GetRegistryValue(RegistryKey key, string name, string value)
    {
        return (string)key.GetValue(name) ?? value;
    }

    private static void SetRegistryValue(RegistryKey key, string name, int value)
    {
        key.SetValue(name, value.ToString());
    }

    private static void SetRegistryValue(RegistryKey key, string name, bool value)
    {
        key.SetValue(name, value ? "1" : "0");
    }

    private static void SetRegistryValue(RegistryKey key, string name, string value)
    {
        key.SetValue(name, value);
    }

    private static Config GetWallpaperConfig()
    {
        RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegPath, true);
        if (key == null)
            throw new("Could not open the registry key.");

        RegistryKey? key2 = Registry.CurrentUser.OpenSubKey(DesktopRegColor, true);
        if (key2 == null)
            throw new("Could not open the registry key.");

        return new()
        {
            Style = GetRegistryValue(key, WallpaperStyleRegPath, 0),
            IsTile = GetRegistryValue(key, TileWallpaperRegPath, false),
            Color = GetRegistryValue(key2, WallpaperStyleRegColor, "#FF0000")
        };
    }

    private static void SetWallpaperConfig(Config value)
    {
        RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegPath, true);
        if (key == null)
            throw new("Could not open the registry key.");

        RegistryKey? key2 = Registry.CurrentUser.OpenSubKey(DesktopRegColor, true);
        if (key2 == null)
            throw new("Could not open the registry key.");

        SetRegistryValue(key, WallpaperStyleRegPath, value.Style);
        SetRegistryValue(key, TileWallpaperRegPath, value.IsTile);
        SetRegistryValue(key2, WallpaperStyleRegColor, value.Color);
    }

    private static void ChangeColor(string color)
    {
        int[] elements = [ColorDesktop];
        int[] colors = [ColorTranslator.ToWin32(ColorTranslator.FromHtml(color))];

        SetSysColors(elements.Length, elements, colors);

        RegistryKey? key = Registry.CurrentUser.OpenSubKey(DesktopRegColor, true);
        key?.SetValue(@"Background", color);
    }

    private static void SetStyle(WallpaperStyle style)
    {
        switch (style)
        {
            case WallpaperStyle.Fill:
                SetWallpaperConfig(new() { Style = 10, IsTile = false, Color = "000000" });
                break;
            case WallpaperStyle.Fit:
                SetWallpaperConfig(new() { Style = 6, IsTile = false, Color = "000000" });
                break;
            case WallpaperStyle.Stretch:
                SetWallpaperConfig(new() { Style = 2, IsTile = false, Color = "000000" });
                break;
            case WallpaperStyle.Tile:
                SetWallpaperConfig(new() { Style = 0, IsTile = true });
                break;
            case WallpaperStyle.Center:
                SetWallpaperConfig(new() { Style = 0, IsTile = false, Color = "000000" });
                break;
            case WallpaperStyle.Span: // Windows 8 or newer only
                SetWallpaperConfig(new() { Style = 22, IsTile = false, Color = "000000" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(style));
        }
    }

    private static void ChangeWallpaper(string? filename)
    {
        SystemParametersInfo(SpiSetdeskwallpaper, 0, filename, SpifUpdateinifile | SpifSendwininichange);
    }

    private static void RestoreHistory()
    {
        if (_historyRestored) return;

        if (!_backupState.HasValue)
            throw new("You must call BackupState() before.");

        State backupState = _backupState.Value;

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(HistoryRegPath, true))
        {
            if (key == null)
                throw new("Could not open the registry key.");

            for (int i = 0; i < HistoryMaxEntries; i++)
                key.SetValue($"BackgroundHistoryPath{i}", backupState.History[i] ?? string.Empty,
                    RegistryValueKind.String);
        }

        _historyRestored = true;
    }

    private static void BackupState()
    {
        string[] history = new string[HistoryMaxEntries];

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(HistoryRegPath, true))
        {
            if (key == null)
                throw new("Could not open the registry key.");

            for (int i = 0; i < history.Length; i++)
                history[i] = (string)key.GetValue($"BackgroundHistoryPath{i}") ?? string.Empty;
        }

        _backupState = new State
        {
            Config = GetWallpaperConfig(),
            History = history,
            Wallpaper = history[0],
            Color = history[1]
        };

        _historyRestored = false;
    }

    public static void RestoreState()
    {
        if (!_backupState.HasValue)
            throw new("You must call BackupState() before.");

        SetWallpaperConfig(_backupState.Value.Config);
        ChangeWallpaper(_backupState.Value.Wallpaper);
        ChangeColor(_backupState.Value.Color);
        RestoreHistory();

        _backupState = null;
    }

    public static void Set(string? filename, string color)
    {
        BackupState();
        ChangeColor(color);
        ChangeWallpaper(filename);
    }

    public static void Set(string? filename, WallpaperStyle style, string color)
    {
        BackupState();
        SetStyle(style);
        ChangeColor(color);
        ChangeWallpaper(filename);
    }

    public static void SilentSet(string? filename, string color)
    {
        Set(filename, color);
        RestoreHistory();
    }

    public static void SilentSet(string filename, WallpaperStyle style, string color)
    {
        Set(filename, style, color);
        RestoreHistory();
    }
}