using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Helpers.Monitoring;

public class ResourceMonitor
{
    private static IResourceProvider? _provider;

    static ResourceMonitor()
    {
        Logger.App("Initializing Resource Monitor");
        Start();
    }

    public static Resource Monitor()
    {
        if (_provider is null)
            return new();
        try
        {
            return _provider.Collect();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public static void Start()
    {
        if (_provider is not null)
            return;

        if (OperatingSystem.IsWindows())
        {
            _provider = CreateWindowsProvider();
        }
        else if (OperatingSystem.IsLinux())
        {
            _provider = CreateLinuxProvider();
        }

        Logger.App("Resource Monitor started");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static IResourceProvider CreateWindowsProvider() => new WindowsResourceProvider();

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static IResourceProvider CreateLinuxProvider() => new LinuxResourceProvider();

    public static void Stop()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
        _provider = null;
        Logger.App("Resource Monitor stopped");
    }

    ~ResourceMonitor()
    {
        Stop();
    }
}
