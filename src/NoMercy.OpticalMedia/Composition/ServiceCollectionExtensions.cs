using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Drives.Backends;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.OpticalMedia.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNoMercyOpticalMedia(this IServiceCollection services)
    {
        services.TryAddSingleton<IDriveBackend>(_ =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return new WindowsDriveBackend();
                }
                catch
                {
                    return new PollingDriveBackend();
                }
            }
            return new PollingDriveBackend();
        });

        services.TryAddSingleton<IDriveMonitor, DriveMonitor>();
        services.TryAddSingleton<DriveLockRegistry>();

        services.TryAddTransient<IDiscScanner, DiscScanner>();
        services.TryAddTransient<IDiscRipper, DiscRipper>();

        return services;
    }
}
