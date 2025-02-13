using System.Diagnostics;
using Newtonsoft.Json;

namespace NoMercy.NmSystem;

public static class UpdateChecker
{
    public static Task StartPeriodicUpdateCheck()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(6));
                Config.UpdateAvailable = IsUpdateAvailable();
            }
            
            // ReSharper disable once FunctionNeverReturns
        });
        
        return Task.CompletedTask;
    }
    
    public static bool IsUpdateAvailable()
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = AppFiles.UpdaterExePath,
                Arguments = "--check",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            string? output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit();

            if (string.IsNullOrEmpty(output)) return false;

            UpdateCheckResult? result = JsonConvert.DeserializeObject<UpdateCheckResult>(output);
            return result?.UpdateAvailable ?? false;
        }
        catch
        {
            return false;
        }
    }

    private class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
    }
}