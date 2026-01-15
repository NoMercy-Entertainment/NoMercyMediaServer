using NoMercy.NmSystem.Information;

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
            return false;
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