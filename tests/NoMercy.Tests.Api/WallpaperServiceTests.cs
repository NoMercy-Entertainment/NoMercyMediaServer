using Microsoft.Extensions.DependencyInjection;
using NoMercy.Helpers;
using NoMercy.Helpers.Wallpaper;
using Xunit;

namespace NoMercy.Tests.Api;

public class WallpaperInterfaceTests
{
    [Fact]
    public void NullWallpaperService_IsNotSupported()
    {
        NullWallpaperService service = new();

        Assert.False(service.IsSupported);
    }

    [Fact]
    public void NullWallpaperService_Set_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(
            () => service.Set("/path/image.jpg", WallpaperStyle.Fill, "#FF0000"));

        Assert.Null(ex);
    }

    [Fact]
    public void NullWallpaperService_SetSilent_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(
            () => service.SetSilent("/path/image.jpg", WallpaperStyle.Stretch, "#00FF00"));

        Assert.Null(ex);
    }

    [Fact]
    public void NullWallpaperService_Restore_DoesNotThrow()
    {
        NullWallpaperService service = new();

        Exception? ex = Record.Exception(() => service.Restore());

        Assert.Null(ex);
    }

    [Fact]
    public void NullWallpaperService_ImplementsInterface()
    {
        NullWallpaperService service = new();

        Assert.IsAssignableFrom<IWallpaperService>(service);
    }
}

public class WallpaperStyleTests
{
    [Theory]
    [InlineData(WallpaperStyle.Fill, 0)]
    [InlineData(WallpaperStyle.Fit, 1)]
    [InlineData(WallpaperStyle.Stretch, 2)]
    [InlineData(WallpaperStyle.Tile, 3)]
    [InlineData(WallpaperStyle.Center, 4)]
    [InlineData(WallpaperStyle.Span, 5)]
    public void WallpaperStyle_HasExpectedValues(WallpaperStyle style, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)style);
    }

    [Fact]
    public void WallpaperStyle_HasSixValues()
    {
        WallpaperStyle[] values = Enum.GetValues<WallpaperStyle>();
        Assert.Equal(6, values.Length);
    }
}

public class LinuxWallpaperStyleMappingTests
{
    [Theory]
    [InlineData(WallpaperStyle.Fill, "zoom")]
    [InlineData(WallpaperStyle.Fit, "scaled")]
    [InlineData(WallpaperStyle.Stretch, "stretched")]
    [InlineData(WallpaperStyle.Tile, "wallpaper")]
    [InlineData(WallpaperStyle.Center, "centered")]
    [InlineData(WallpaperStyle.Span, "spanned")]
    public void MapStyleToGnome_ReturnsCorrectMapping(
        WallpaperStyle input, string expected)
    {
        string result = LinuxWallpaperService.MapStyleToGnome(input);
        Assert.Equal(expected, result);
    }
}

public class LinuxDesktopDetectionTests
{
    [Fact]
    public void DetectDesktopEnvironment_WithNoEnvVar_ReturnsFallback()
    {
        string? original = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", null);
            LinuxWallpaperService.DesktopEnvironment result =
                LinuxWallpaperService.DetectDesktopEnvironment();
            Assert.Equal(LinuxWallpaperService.DesktopEnvironment.Fallback, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", original);
        }
    }

    [Theory]
    [InlineData("GNOME", LinuxWallpaperService.DesktopEnvironment.Gnome)]
    [InlineData("ubuntu:GNOME", LinuxWallpaperService.DesktopEnvironment.Gnome)]
    [InlineData("UNITY", LinuxWallpaperService.DesktopEnvironment.Gnome)]
    [InlineData("KDE", LinuxWallpaperService.DesktopEnvironment.Kde)]
    [InlineData("XFCE", LinuxWallpaperService.DesktopEnvironment.Xfce)]
    [InlineData("MATE", LinuxWallpaperService.DesktopEnvironment.Fallback)]
    public void DetectDesktopEnvironment_ReturnsExpected(
        string envValue,
        LinuxWallpaperService.DesktopEnvironment expected)
    {
        string? original = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", envValue);
            LinuxWallpaperService.DesktopEnvironment result =
                LinuxWallpaperService.DetectDesktopEnvironment();
            Assert.Equal(expected, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", original);
        }
    }
}

public class WindowsHexToColorTests
{
    [Theory]
    [InlineData("#FF0000", 0x000000FF)]  // Red: R=255, G=0, B=0 → 0x00_00_00_FF
    [InlineData("#00FF00", 0x0000FF00)]  // Green: R=0, G=255, B=0 → 0x00_00_FF_00
    [InlineData("#0000FF", 0x00FF0000)]  // Blue: R=0, G=0, B=255 → 0x00_FF_00_00
    [InlineData("#FFFFFF", 0x00FFFFFF)]  // White
    [InlineData("#000000", 0x00000000)]  // Black
    [InlineData("FF8040", 0x004080FF)]   // Without #
    public void HexToWin32Color_ConvertsCorrectly(string hex, int expected)
    {
        int result = WindowsWallpaperService.HexToWin32Color(hex);
        Assert.Equal(expected, result);
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
        IWallpaperService? service = provider.GetService(typeof(IWallpaperService)) as IWallpaperService;

        Assert.NotNull(service);
    }

    [Fact]
    public void AddWallpaperService_OnLinuxWithoutDisplay_RegistersNullService()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return; // Skip on non-Linux
        }

        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", null);
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);

            ServiceCollection services = new();
            services.AddWallpaperService();

            ServiceProvider provider =
                services.BuildServiceProvider();
            IWallpaperService service = (IWallpaperService)provider
                .GetService(typeof(IWallpaperService))!;

            Assert.IsType<NullWallpaperService>(service);
            Assert.False(service.IsSupported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", display);
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", wayland);
        }
    }
}
