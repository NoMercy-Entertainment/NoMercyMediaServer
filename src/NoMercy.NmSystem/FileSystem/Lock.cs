using System.Diagnostics;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public static class Locking
{
    public static bool IsFileLocked(string filePath)
    {
        try
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Close();
        }
        catch (IOException)
        {
            return true;
        }

        return false;
    }

    public static void CloseApplicationLockingFile(string filePath)
    {
        Logger.Setup($"Closing application locking {filePath}", LogEventLevel.Verbose);

        foreach (Process process in Process.GetProcesses())
            try
            {
                if (process.MainModule?.FileName == null) continue;
                if (!process.MainModule.FileName.Equals(filePath, StringComparison.OrdinalIgnoreCase)) continue;
                
                process.Kill();
                process.WaitForExit();
                
                Logger.System($"Closed application {process.ProcessName} locking {filePath}", LogEventLevel.Verbose);
                
                break;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Ignore the error if the process is not accessible
            }
            catch (InvalidOperationException ex)
            {
                Logger.System($"Process {process.ProcessName} has already exited: {ex.Message}", LogEventLevel.Warning);
            }
    }
}