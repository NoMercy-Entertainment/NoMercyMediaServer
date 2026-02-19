using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup;

public class ApiInfo
{
    public static string MakeMkvKey { get; set; } = string.Empty;
    public static string TmdbKey { get; set; } = string.Empty;
    public static string OmdbKey { get; set; } = string.Empty;
    public static string FanArtApiKey { get; set; } = string.Empty;
    public static string RottenTomatoes { get; set; } = string.Empty;
    public static string AcousticIdKey { get; set; } = string.Empty;
    public static string TadbKey { get; set; } = string.Empty;
    public static string TmdbToken { get; set; } = string.Empty;
    public static string TvdbKey { get; set; } = string.Empty;
    public static string MusixmatchKey { get; set; } = string.Empty;
    public static string JwplayerKey { get; set; } = string.Empty;

    // :TODO Make the fanart client key configurable in the dashboard
    public static string FanArtClientKey { get; set; } = string.Empty;

    public static string[] Colors { get; private set; } =
    [
        "#8f00fc",
        "#705BAD",
        "#CBAFFF"
    ];

    public static string Quote { get; private set; } = string.Empty;

    public static bool KeysLoaded { get; private set; }

    internal static string CacheFilePath =>
        Path.Combine(AppFiles.ConfigPath, "api_keys.json");

    private static readonly int[] BackoffSeconds = [30, 60, 300, 900, 1800];

    public static async Task RequestInfo()
    {
        // 1. Try network first
        ApiInfoResponse? liveData = await TryFetchFromNetwork();

        if (liveData is not null)
        {
            ApplyKeys(liveData);
            await WriteCacheFile(liveData);
            Logger.Setup("API keys loaded from network");
            return;
        }

        // 2. Network failed — try cache
        ApiInfoResponse? cachedData = await TryReadCacheFile();

        if (cachedData is not null)
        {
            ApplyKeys(cachedData);
            string cachedAt = cachedData.CachedAt ?? "unknown";

            DateTime? cachedAtDate = cachedData.CachedAt is not null
                ? DateTime.TryParse(cachedData.CachedAt, out DateTime parsed) ? parsed : null
                : null;

            if (cachedAtDate.HasValue &&
                (DateTime.UtcNow - cachedAtDate.Value).TotalDays > 30)
            {
                Logger.Setup(
                    $"API keys loaded from cache (cached at {cachedAt}) — cache is over 30 days old",
                    LogEventLevel.Warning);
            }
            else
            {
                Logger.Setup(
                    $"API keys loaded from cache (cached at {cachedAt})",
                    LogEventLevel.Warning);
            }

            StartBackgroundRefresh();
            return;
        }

        // 3. No network, no cache — cannot function without keys
        Logger.Setup(
            "API unreachable and no cached keys available — provider features will be unavailable",
            LogEventLevel.Error);
    }

    internal static async Task<ApiInfoResponse?> TryFetchFromNetwork()
    {
        try
        {
            Logger.Setup("Requesting server info");

            GenericHttpClient apiClient = new(Config.ApiBaseUrl);
            apiClient.SetDefaultHeaders(Config.UserAgent);

            string content = await apiClient.SendAndReadAsync(HttpMethod.Get, "v1/info");

            ApiInfoResponse? data = content.FromJson<ApiInfoResponse>();
            if (data?.Data?.Keys is null)
                return null;

            return data;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to fetch API keys from network: {ex.Message}",
                LogEventLevel.Warning);
            return null;
        }
    }

    internal static void ApplyKeys(ApiInfoResponse data)
    {
        Quote = data.Data.Quote;
        Colors = data.Data.Colors;

        MakeMkvKey = data.Data.Keys.MakeMkvKey;
        TmdbKey = data.Data.Keys.TmdbKey;
        OmdbKey = data.Data.Keys.OmdbKey;
        FanArtApiKey = data.Data.Keys.FanArtKey;
        RottenTomatoes = data.Data.Keys.RottenTomatoes;
        AcousticIdKey = data.Data.Keys.AcousticIdKey;
        TadbKey = data.Data.Keys.TadbKey;
        TmdbToken = data.Data.Keys.TmdbToken;
        TvdbKey = data.Data.Keys.TvdbKey;
        MusixmatchKey = data.Data.Keys.MusixmatchKey;
        JwplayerKey = data.Data.Keys.JwplayerKey;

        KeysLoaded = true;
    }

    internal static async Task WriteCacheFile(ApiInfoResponse data)
    {
        try
        {
            data.CachedAt = DateTime.UtcNow.ToString("O");
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await File.WriteAllTextAsync(CacheFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to write API keys cache: {ex.Message}",
                LogEventLevel.Warning);
        }
    }

    internal static async Task<ApiInfoResponse?> TryReadCacheFile()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
                return null;

            string json = await File.ReadAllTextAsync(CacheFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            ApiInfoResponse? data = json.FromJson<ApiInfoResponse>();
            if (data?.Data?.Keys is null)
                return null;

            return data;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to read API keys cache: {ex.Message}",
                LogEventLevel.Warning);
            return null;
        }
    }

    internal static void StartBackgroundRefresh()
    {
        _ = Task.Run(async () =>
        {
            int attempt = 0;

            while (true)
            {
                int delay = BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)];
                await Task.Delay(TimeSpan.FromSeconds(delay));

                ApiInfoResponse? fresh = await TryFetchFromNetwork();
                if (fresh is not null)
                {
                    ApplyKeys(fresh);
                    await WriteCacheFile(fresh);
                    Logger.Setup("API keys refreshed from network");
                    return;
                }

                attempt++;
                Logger.Setup(
                    $"API key refresh attempt {attempt} failed, retrying in {delay}s",
                    LogEventLevel.Warning);
            }
        });
    }
}
