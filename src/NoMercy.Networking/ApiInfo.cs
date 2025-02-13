using Newtonsoft.Json;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Networking;

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
        HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(120);
        client.BaseAddress = new(Config.ApiBaseUrl);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        
        HttpResponseMessage response = await client.GetAsync("v1/info");
        if (!response.IsSuccessStatusCode)
        {
            throw new("The NoMercy API is not available");
        }
        
        string? content = await response.Content.ReadAsStringAsync();

        if (content == null) throw new("Failed to get server info");

        try
        {
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
            Logger.Setup(content, LogEventLevel.Error);
            Logger.Setup(e.Message, LogEventLevel.Error);
            throw;
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
