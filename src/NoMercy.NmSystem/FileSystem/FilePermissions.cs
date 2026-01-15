using System.Runtime.InteropServices;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public class FilePermissions
{
    public static async Task SetExecutionPermissions(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (File.Exists(path))
                await Shell.ExecAsync("chmod", $"+x \"{path}\"");
                
            else if (Directory.Exists(path))
                await Shell.ExecAsync("chmod", $"-R +x \"{path}\"");

            Logger.System($"Set execution permissions for {path}", LogEventLevel.Verbose);
        }
    }
}