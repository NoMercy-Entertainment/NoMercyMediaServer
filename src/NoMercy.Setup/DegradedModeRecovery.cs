using System.IdentityModel.Tokens.Jwt;
using NoMercy.Networking;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup;

public class DeferredTasks
{
    public bool ApiKeysLoaded { get; set; }
    public bool Authenticated { get; set; }
    public bool NetworkDiscovered { get; set; }
    public bool Registered { get; set; }
    public bool SeedsRun { get; set; }
    public bool AllCompleted { get; set; }

    public List<TaskDelegate> CallerTasks { get; set; } = [];
}

public static class DegradedModeRecovery
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
    ];

    public static async Task StartRecoveryLoop(DeferredTasks tasks)
    {
        int attempt = 0;

        while (!tasks.AllCompleted)
        {
            TimeSpan delay = BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)];
            await Task.Delay(delay);

            bool hasNetwork = await NetworkProbe.CheckConnectivity();
            if (!hasNetwork)
            {
                attempt++;
                Logger.App(
                    $"Network still unavailable. Next retry in {BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)]}"
                );
                continue;
            }

            Logger.App("Network connectivity restored — executing deferred tasks");

            if (!tasks.ApiKeysLoaded)
            {
                try
                {
                    await ApiInfo.RequestInfo();
                    tasks.ApiKeysLoaded = ApiInfo.KeysLoaded;
                }
                catch (Exception e)
                {
                    Logger.App($"Deferred ApiInfo failed: {e.Message}", LogEventLevel.Warning);
                }
            }

            if (!tasks.Authenticated && tasks.ApiKeysLoaded)
            {
                string? token = Globals.Globals.AccessToken;
                if (string.IsNullOrEmpty(token))
                {
                    // Auth not ready — AuthManager background refresh will handle it
                    Logger.App(
                        "Auth not ready — waiting for AuthManager background refresh",
                        LogEventLevel.Verbose
                    );
                }
                else
                {
                    tasks.Authenticated = true;
                }
            }

            if (!tasks.NetworkDiscovered)
            {
                try
                {
                    if (Start.NetworkDiscovery is not null)
                        await Start.NetworkDiscovery.DiscoverExternalIpAsync();
                    tasks.NetworkDiscovered = true;
                }
                catch (Exception e)
                {
                    Logger.App(
                        $"Deferred network discovery failed: {e.Message}",
                        LogEventLevel.Warning
                    );
                }
            }

            if (!tasks.SeedsRun && tasks.ApiKeysLoaded)
            {
                try
                {
                    foreach (TaskDelegate callerTask in tasks.CallerTasks)
                    {
                        await callerTask.Invoke();
                    }
                    tasks.SeedsRun = true;
                }
                catch (Exception e)
                {
                    Logger.App($"Deferred seeds failed: {e.Message}", LogEventLevel.Warning);
                }
            }

            if (!tasks.Registered && tasks.Authenticated && tasks.NetworkDiscovered)
            {
                // Skip if RunPostAuthRegistration() already completed registration
                if (Register.IsRegistered)
                {
                    tasks.Registered = true;
                }
                else
                {
                    try
                    {
                        // Ensure token is present and not expired before attempting registration.
                        // AuthManager background refresh keeps the token alive; a null/empty check
                        // is not sufficient — nomercy-tv will reject an expired JWT.
                        bool tokenNeedsRefresh = true;

                        string? registrationToken = Globals.Globals.AccessToken;
                        if (!string.IsNullOrEmpty(registrationToken))
                        {
                            try
                            {
                                JwtSecurityTokenHandler tokenHandler = new();
                                JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(
                                    registrationToken
                                );
                                tokenNeedsRefresh =
                                    parsedToken.ValidTo <= DateTime.UtcNow.AddSeconds(30);
                            }
                            catch
                            {
                                // Token could not be parsed — treat as expired
                            }
                        }

                        if (tokenNeedsRefresh)
                        {
                            Logger.App(
                                "Access token missing or expired before deferred registration — waiting for AuthManager background refresh",
                                LogEventLevel.Warning
                            );
                            // Auth not ready — AuthManager background refresh will handle it
                            continue;
                        }

                        await Register.Init();
                        tasks.Registered = true;
                    }
                    catch (InvalidOperationException e) when (e.Message.Contains("cooldown"))
                    {
                        // Cooldown active — will retry on next loop iteration
                        Logger.App(
                            $"Deferred registration deferred: {e.Message}",
                            LogEventLevel.Debug
                        );
                    }
                    catch (Exception e)
                    {
                        Logger.App(
                            $"Deferred registration failed: {e.Message}",
                            LogEventLevel.Warning
                        );
                    }
                }
            }

            if (
                tasks.ApiKeysLoaded
                && tasks.Authenticated
                && tasks.NetworkDiscovered
                && tasks.SeedsRun
                && tasks.Registered
            )
            {
                tasks.AllCompleted = true;
                Logger.App("Full mode restored — all deferred tasks completed");
            }

            attempt++;
        }
    }
}
