using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup;

public partial class ApiInfo
{
    public static readonly string ApplicationVersion = Environment.Version.ToString();
    public static readonly string ApplicationName = "NoMercy MediaServer";

    public static string MakeMkvKey { get; set; } = string.Empty;
    public static string TmdbKey { get; set; } = string.Empty;
    public static string OmdbKey { get; set; } = string.Empty;
    public static string FanArtKey { get; set; } = string.Empty;
    public static string RottenTomatoes { get; set; } = string.Empty;
    public static string AcousticIdKey { get; set; } = string.Empty;
    public static string TadbKey { get; set; } = string.Empty;
    public static string TmdbToken { get; set; } = string.Empty;
    public static string TvdbKey { get; set; } = string.Empty;
    public static string MusixmatchKey { get; set; } = string.Empty;
    public static string JwplayerKey { get; set; } = string.Empty;
    public static Downloads BinaryList { get; private set; } = new();
    public static string[] Colors { get; private set; } = [
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

            ApiInfo? data = content.FromJson<ApiInfo>();
            if (data == null) throw new("Failed to deserialize server info");

            Quote = data.Data.Quote;
            Colors = data.Data.Colors;

            BinaryList = data.Data.Downloads;

            MakeMkvKey = data.Data.Keys.MakeMkvKey;
            TmdbKey = data.Data.Keys.TmdbKey;
            OmdbKey = data.Data.Keys.OmdbKey;
            FanArtKey = data.Data.Keys.FanArtKey;
            RottenTomatoes = data.Data.Keys.RottenTomatoes;
            AcousticIdKey = data.Data.Keys.AcousticId;
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

#region Types

public partial class ApiInfo
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("data")] public Data Data { get; set; } = new();
}
#endregion
