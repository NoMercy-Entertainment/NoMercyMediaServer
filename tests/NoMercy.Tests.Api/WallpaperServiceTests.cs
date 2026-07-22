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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Wallpaper;
using Xunit;

namespace NoMercy.Tests.Api;

public class WallpaperInterfaceTests
{
    [Fact]
    public void NullWallpaperService_IsNotSupported()
    {
        NullWallpaperService service = new();

        Assert.False(condition: service.IsSupported);
    }

    [Fact]
    public void NullWallpaperService_Set_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(testCode: () =>
            service.Set(imagePath: "/path/image.jpg", style: WallpaperStyle.Fill, hexColor: "#FF0000")
        );

        Assert.Null(@object: ex);
    }

    [Fact]
    public void NullWallpaperService_SetSilent_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(testCode: () =>
            service.SetSilent(imagePath: "/path/image.jpg", style: WallpaperStyle.Stretch, hexColor: "#00FF00")
        );

        Assert.Null(@object: ex);
    }

    [Fact]
    public void NullWallpaperService_Restore_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(testCode: () => service.Restore());

        Assert.Null(@object: ex);
    }

    [Fact]
    public void NullWallpaperService_ImplementsInterface()
    {
        NullWallpaperService service = new();

        Assert.IsAssignableFrom<IWallpaperService>(@object: service);
    }
}

public class WallpaperStyleTests
{
    [Theory]
    [InlineData(data: [WallpaperStyle.Fill, 0])]
    [InlineData(data: [WallpaperStyle.Fit, 1])]
    [InlineData(data: [WallpaperStyle.Stretch, 2])]
    [InlineData(data: [WallpaperStyle.Tile, 3])]
    [InlineData(data: [WallpaperStyle.Center, 4])]
    [InlineData(data: [WallpaperStyle.Span, 5])]
    public void WallpaperStyle_HasExpectedValues(WallpaperStyle style, int expectedValue)
    {
        Assert.Equal(expected: expectedValue, actual: (int)style);
    }

    [Fact]
    public void WallpaperStyle_HasSixValues()
    {
        WallpaperStyle[] values = Enum.GetValues<WallpaperStyle>();
        Assert.Equal(expected: 6, actual: values.Length);
    }
}

[SupportedOSPlatform(platformName: "linux")]
public class LinuxWallpaperStyleMappingTests
{
    [Theory]
    [InlineData(data: [WallpaperStyle.Fill, "zoom"])]
    [InlineData(data: [WallpaperStyle.Fit, "scaled"])]
    [InlineData(data: [WallpaperStyle.Stretch, "stretched"])]
    [InlineData(data: [WallpaperStyle.Tile, "wallpaper"])]
    [InlineData(data: [WallpaperStyle.Center, "centered"])]
    [InlineData(data: [WallpaperStyle.Span, "spanned"])]
    public void MapStyleToGnome_ReturnsCorrectMapping(WallpaperStyle input, string expected)
    {
        string result = LinuxWallpaperService.MapStyleToGnome(style: input);
        Assert.Equal(expected: expected, actual: result);
    }
}

[SupportedOSPlatform(platformName: "linux")]
public class LinuxDesktopDetectionTests
{
    [Fact]
    public void DetectDesktopEnvironment_WithNoEnvVar_ReturnsFallback()
    {
        string? original = Environment.GetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP");
        try
        {
            Environment.SetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP", value: null);
            LinuxWallpaperService.DesktopEnvironment result =
                LinuxWallpaperService.DetectDesktopEnvironment();
            Assert.Equal(expected: LinuxWallpaperService.DesktopEnvironment.Fallback, actual: result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP", value: original);
        }
    }

    [Theory]
    [InlineData(data: ["GNOME", LinuxWallpaperService.DesktopEnvironment.Gnome])]
    [InlineData(data: ["ubuntu:GNOME", LinuxWallpaperService.DesktopEnvironment.Gnome])]
    [InlineData(data: ["UNITY", LinuxWallpaperService.DesktopEnvironment.Gnome])]
    [InlineData(data: ["KDE", LinuxWallpaperService.DesktopEnvironment.Kde])]
    [InlineData(data: ["XFCE", LinuxWallpaperService.DesktopEnvironment.Xfce])]
    [InlineData(data: ["MATE", LinuxWallpaperService.DesktopEnvironment.Fallback])]
    public void DetectDesktopEnvironment_ReturnsExpected(
        string envValue,
        LinuxWallpaperService.DesktopEnvironment expected
    )
    {
        string? original = Environment.GetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP");
        try
        {
            Environment.SetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP", value: envValue);
            LinuxWallpaperService.DesktopEnvironment result =
                LinuxWallpaperService.DetectDesktopEnvironment();
            Assert.Equal(expected: expected, actual: result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "XDG_CURRENT_DESKTOP", value: original);
        }
    }
}

[SupportedOSPlatform(platformName: "windows")]
public class WindowsHexToColorTests
{
    [Theory]
    [InlineData(data: ["#FF0000", 0x000000FF])] // Red: R=255, G=0, B=0 → 0x00_00_00_FF
    [InlineData(data: ["#00FF00", 0x0000FF00])] // Green: R=0, G=255, B=0 → 0x00_00_FF_00
    [InlineData(data: ["#0000FF", 0x00FF0000])] // Blue: R=0, G=0, B=255 → 0x00_FF_00_00
    [InlineData(data: ["#FFFFFF", 0x00FFFFFF])] // White
    [InlineData(data: ["#000000", 0x00000000])] // Black
    [InlineData(data: ["FF8040", 0x004080FF])] // Without #
    public void HexToWin32Color_ConvertsCorrectly(string hex, int expected)
    {
        int result = WindowsWallpaperService.HexToWin32Color(hex: hex);
        Assert.Equal(expected: expected, actual: result);
    }
}

public class WallpaperDiRegistrationTests
{
    [Fact]
    public void AddWallpaperService_RegistersService()
    {
        ServiceCollection services = new();

        services.AddWallpaperService();

        ServiceProvider provider = services.BuildServiceProvider();
        IWallpaperService? service =
            provider.GetService(serviceType: typeof(IWallpaperService)) as IWallpaperService;

        Assert.NotNull(@object: service);
    }

    [Fact]
    public void AddWallpaperService_OnLinuxWithoutDisplay_RegistersNullService()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
        {
            return; // Skip on non-Linux
        }

        string? display = Environment.GetEnvironmentVariable(variable: "DISPLAY");
        string? wayland = Environment.GetEnvironmentVariable(variable: "WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable(variable: "DISPLAY", value: null);
            Environment.SetEnvironmentVariable(variable: "WAYLAND_DISPLAY", value: null);

            ServiceCollection services = new();
            services.AddWallpaperService();

            ServiceProvider provider = services.BuildServiceProvider();
            IWallpaperService service = (IWallpaperService)
                provider.GetService(serviceType: typeof(IWallpaperService))!;

            Assert.IsType<NullWallpaperService>(@object: service);
            Assert.False(condition: service.IsSupported);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: "DISPLAY", value: display);
            Environment.SetEnvironmentVariable(variable: "WAYLAND_DISPLAY", value: wayland);
        }
    }
}
