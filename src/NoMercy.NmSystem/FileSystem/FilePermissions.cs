using System.Runtime.InteropServices;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public class FilePermissions
{
    public static async Task SetExecutionPermissions(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // LOCAL-ONLY: FilePermissions is a static class in NmSystem; no reference to NoMercy.Providers.
            IStorageBackend backend = new SystemIoStorageBackend();

            if (backend.FileExists(path))
                await Shell.ExecAsync("chmod", $"+x \"{path}\"");
            else if (backend.DirectoryExists(path))
                await Shell.ExecAsync("chmod", $"-R +x \"{path}\"");

            Logger.System($"Set execution permissions for {path}", LogEventLevel.Verbose);
        }
    }
}
