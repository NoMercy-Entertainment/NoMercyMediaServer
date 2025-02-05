using System.Diagnostics;
using NoMercy.Updater;

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
                Config.UpdateAvailable =  await NoMercyUpdater.CheckForUpdate();
            }
            
            // ReSharper disable once FunctionNeverReturns
        });
        
        return Task.CompletedTask;
    }
    
    public static bool IsUpdateAvailable()
    {
        try
        {
            return NoMercyUpdater.CheckForUpdate().Result;
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