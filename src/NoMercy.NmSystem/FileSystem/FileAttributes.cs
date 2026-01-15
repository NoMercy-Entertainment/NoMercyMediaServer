using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public static class FileAttributes
{
    public static Task SetCreatedAttribute(string filePath, DateTimeOffset createdAt)
    {
        try
        {
            File.SetCreationTimeUtc(filePath, createdAt.UtcDateTime);

            Logger.System($"Set creation and modification dates for {filePath} to {createdAt}", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.System($"Failed to set file attributes for {filePath}: {ex.Message}", LogEventLevel.Warning);
        }
        
        return Task.CompletedTask;
    }
}