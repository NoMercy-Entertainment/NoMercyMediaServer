using System.Runtime.InteropServices;

namespace NoMercy.NmSystem.Information;

public static class Screen
{
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

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))) return false;

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
    }
}