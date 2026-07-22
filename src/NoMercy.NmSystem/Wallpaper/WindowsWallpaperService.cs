// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.NmSystem.Wallpaper;

[SupportedOSPlatform(platformName: "windows")]
public class WindowsWallpaperService : IWallpaperService
{
    private const string DesktopRegPath = @"Control Panel\Desktop";
    private const string DesktopRegColor = @"Control Panel\Colors";
    private const string HistoryRegPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
    private const string WallpaperStyleRegPath = "WallpaperStyle";
    private const string WallpaperStyleRegColor = "WallpaperColor";
    private const string TileWallpaperRegPath = "TileWallpaper";

    private const int HistoryMaxEntries = 5;
    private const int ColorDesktop = 1;
    private const int SpiSetdeskwallpaper = 20;
    private const int SpifUpdateinifile = 0x01;
    private const int SpifSendwininichange = 0x02;

    [DllImport(dllName: "user32.dll")]
    private static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

    [DllImport(dllName: "user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(
        int uAction,
        int uParam,
        string? lpvParam,
        int fuWinIni
    );

    private BackupState? _backup;
    private bool _historyRestored;

    public bool IsSupported => true;

    private struct WallpaperConfig
    {
        public int Style;
        public bool IsTile;
        public string Color;
    }

    private struct BackupState
    {
        public WallpaperConfig Config;
        public string?[] History;
        public string Wallpaper;
        public string Color;
    }

    public void Set(string imagePath, WallpaperStyle style, string hexColor)
    {
        SaveBackup();
        ApplyStyle(style: style);
        ApplyColor(hexColor: hexColor);
        ApplyWallpaper(filename: imagePath);
    }

    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor)
    {
        Set(imagePath: imagePath, style: style, hexColor: hexColor);
        RestoreHistory();
    }

    public void Restore()
    {
        if (!_backup.HasValue)
            return;

        SetWallpaperConfig(config: _backup.Value.Config);
        ApplyWallpaper(filename: _backup.Value.Wallpaper);
        ApplyColor(hexColor: _backup.Value.Color);
        RestoreHistory();

        _backup = null;
    }

    private void SaveBackup()
    {
        string[] history = new string[HistoryMaxEntries];

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(name: HistoryRegPath, writable: false))
        {
            if (key is not null)
            {
                for (int i = 0; i < history.Length; i++)
                    history[i] = ((string?)key.GetValue(name: $"BackgroundHistoryPath{i}")).OrEmpty();
            }
        }

        _backup = new BackupState
        {
            Config = GetWallpaperConfig(),
            History = history,
            Wallpaper = history[0],
            Color = history.Length > 1 ? history[1] : string.Empty,
        };

        _historyRestored = false;
    }

    private void RestoreHistory()
    {
        if (_historyRestored)
            return;
        if (!_backup.HasValue)
            return;

        BackupState state = _backup.Value;

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(name: HistoryRegPath, writable: true);
        if (key is null)
            return;

        for (int i = 0; i < HistoryMaxEntries; i++)
            key.SetValue(
                name: $"BackgroundHistoryPath{i}",
                value: state.History[i].OrEmpty(),
                valueKind: RegistryValueKind.String
            );

        _historyRestored = true;
    }

    private static WallpaperConfig GetWallpaperConfig()
    {
        using RegistryKey? desktopKey = Registry.CurrentUser.OpenSubKey(name: DesktopRegPath, writable: false);
        using RegistryKey? colorKey = Registry.CurrentUser.OpenSubKey(name: DesktopRegColor, writable: false);

        return new()
        {
            Style = ParseRegistryInt(key: desktopKey, name: WallpaperStyleRegPath, defaultValue: 0),
            IsTile = ParseRegistryBool(key: desktopKey, name: TileWallpaperRegPath, defaultValue: false),
            Color = ParseRegistryString(key: colorKey, name: WallpaperStyleRegColor, defaultValue: "#FF0000"),
        };
    }

    private static void SetWallpaperConfig(WallpaperConfig config)
    {
        using RegistryKey? desktopKey = Registry.CurrentUser.OpenSubKey(name: DesktopRegPath, writable: true);
        using RegistryKey? colorKey = Registry.CurrentUser.OpenSubKey(name: DesktopRegColor, writable: true);

        desktopKey?.SetValue(name: WallpaperStyleRegPath, value: config.Style.ToString());
        desktopKey?.SetValue(name: TileWallpaperRegPath, value: config.IsTile ? "1" : "0");
        colorKey?.SetValue(name: WallpaperStyleRegColor, value: config.Color);
    }

    private static void ApplyColor(string hexColor)
    {
        int colorValue = HexToWin32Color(hex: hexColor);
        int[] elements = [ColorDesktop];
        int[] colors = [colorValue];
        SetSysColors(cElements: elements.Length, lpaElements: elements, lpaRgbValues: colors);

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(name: DesktopRegColor, writable: true);
        key?.SetValue(name: "Background", value: hexColor);
    }

    private static void ApplyStyle(WallpaperStyle style)
    {
        WallpaperConfig config = style switch
        {
            WallpaperStyle.Fill => new()
            {
                Style = 10,
                IsTile = false,
                Color = "000000",
            },
            WallpaperStyle.Fit => new()
            {
                Style = 6,
                IsTile = false,
                Color = "000000",
            },
            WallpaperStyle.Stretch => new()
            {
                Style = 2,
                IsTile = false,
                Color = "000000",
            },
            WallpaperStyle.Tile => new()
            {
                Style = 0,
                IsTile = true,
                Color = "000000",
            },
            WallpaperStyle.Center => new()
            {
                Style = 0,
                IsTile = false,
                Color = "000000",
            },
            WallpaperStyle.Span => new()
            {
                Style = 22,
                IsTile = false,
                Color = "000000",
            },
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(style)),
        };

        SetWallpaperConfig(config: config);
    }

    private static void ApplyWallpaper(string? filename)
    {
        SystemParametersInfo(
            uAction: SpiSetdeskwallpaper,
            uParam: 0,
            lpvParam: filename,
            fuWinIni: SpifUpdateinifile | SpifSendwininichange
        );
    }

    public static int HexToWin32Color(string hex)
    {
        string clean = hex.TrimStart(trimChar: '#');
        if (clean.Length < 6)
            clean = clean.PadRight(totalWidth: 6, paddingChar: '0');

        int r = Convert.ToInt32(value: clean.Substring(startIndex: 0, length: 2), fromBase: 16);
        int g = Convert.ToInt32(value: clean.Substring(startIndex: 2, length: 2), fromBase: 16);
        int b = Convert.ToInt32(value: clean.Substring(startIndex: 4, length: 2), fromBase: 16);

        // Win32 COLORREF is 0x00BBGGRR
        return r | (g << 8) | (b << 16);
    }

    private static int ParseRegistryInt(RegistryKey? key, string name, int defaultValue)
    {
        string? value = key?.GetValue(name: name) as string;
        return int.TryParse(s: value, result: out int result) ? result : defaultValue;
    }

    private static bool ParseRegistryBool(RegistryKey? key, string name, bool defaultValue)
    {
        string? value = key?.GetValue(name: name) as string;
        return value is not null ? value == "1" : defaultValue;
    }

    private static string ParseRegistryString(RegistryKey? key, string name, string defaultValue)
    {
        return key?.GetValue(name: name) as string ?? defaultValue;
    }
}
