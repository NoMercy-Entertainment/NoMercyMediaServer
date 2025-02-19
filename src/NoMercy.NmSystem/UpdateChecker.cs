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
            string output = SystemCalls.Shell.ExecStdOutSync(AppFiles.UpdaterExePath, "--check");

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