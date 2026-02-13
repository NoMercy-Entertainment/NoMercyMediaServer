using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.Helpers.Wallpaper;

public static class WallpaperServiceExtensions
{
    public static IServiceCollection AddWallpaperService(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            services.AddSingleton<IWallpaperService, WindowsWallpaperService>();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            services.AddSingleton<IWallpaperService, MacWallpaperService>();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            bool hasDisplay =
                Environment.GetEnvironmentVariable("DISPLAY") is not null
                || Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null;

            if (hasDisplay)
                services.AddSingleton<IWallpaperService, LinuxWallpaperService>();
            else
                services.AddSingleton<IWallpaperService, NullWallpaperService>();
        }
        else
        {
            services.AddSingleton<IWallpaperService, NullWallpaperService>();
        }

        return services;
    }
}
