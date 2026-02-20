using System.Runtime.InteropServices;

namespace NoMercy.NmSystem.Information;

public static class Screen
{
    public static bool IsDocker =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));

    public static int ScreenWidth()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ScreenWidthWindows() : 1666;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static int ScreenWidthWindows(int screenIndex = 0)
    {
        return GetSystemMetrics(screenIndex);
    }

    public static bool IsDesktopEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return true;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;

        if (IsDocker) return false;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))) return false;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) return true;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return true;

        string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return sessionType is "x11" or "wayland";
    }
}
