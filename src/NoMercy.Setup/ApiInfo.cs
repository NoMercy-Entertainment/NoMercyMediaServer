using NoMercy.NmSystem;
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

    public static async Task RequestInfo()
    {
        try
        {
            Logger.Setup("Requesting server info");
            
            GenericHttpClient apiClient = new(Config.ApiBaseUrl);
            apiClient.SetDefaultHeaders(Config.UserAgent);
            
            string content = await apiClient.SendAndReadAsync(HttpMethod.Get, "v1/info");

            if (content == null) throw new("Failed to get server info");

            ApiInfoResponse? data = content.FromJson<ApiInfoResponse>();
            if (data == null) throw new("Failed to deserialize server info");

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
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
            Logger.App("Shutting down application");
            Environment.Exit(1);
        }
    }
}
