using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Information;

public static class Config
{
    private const string DefaultAuthBaseUrl = "https://auth.nomercy.tv/realms/NoMercyTV/";
    private const string DefaultAppBaseUrl = "https://app.nomercy.tv/";
    private const string DefaultApiBaseUrl = "https://api.nomercy.tv/";

    public static string AuthBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_AUTH_URL") ?? DefaultAuthBaseUrl;

    public static readonly string TokenClientId = "nomercy-server";

    public static string AppBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_APP_URL") ?? DefaultAppBaseUrl;

    public static string ApiBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_API_URL") ?? DefaultApiBaseUrl;

    public static string ApiServerBaseUrl { get; set; } = $"{ApiBaseUrl}v1/server/";

    public static readonly string DnsServer = "1.1.1.1";

    public static string UserAgent => $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    public static bool Started { get; set; }
    public static string? CloudflareTunnelToken { get; set; }

    public static NatStatus NatStatus { get; set; } = NatStatus.None;
    public static bool PortForwarded { get; set; }
    
    private static int? _internalServerPort = null;

    public static int InternalServerPort
    {
        get => _internalServerPort ?? 7626;
        set => _internalServerPort = value;
    }
    
    private static int? _externalServerPort = null;

    public static int ExternalServerPort
    {
        get => _externalServerPort ?? 7626;
        set => _externalServerPort = value;
    }

    private static int? _managementPort = null;

    public static int ManagementPort
    {
        get => _managementPort ?? 7627;
        set => _managementPort = value;
    }

    public static bool Swagger { get; set; } = true;

    public static bool Sentry { get; set; }
    public static string SentryDsn { get; set; } = string.Empty;

    public static bool IsDev { get; set; }
    public static bool UpdateAvailable { get; set; }

    public static KeyValuePair<string, int> QueueWorkers { get; set; } = new("queue", 1);
    public static KeyValuePair<string, int> EncoderWorkers { get; set; } = new("encoder", 2);
    public static KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);
    public static KeyValuePair<string, int> DataWorkers { get; set; } = new("data", 10);
    public static KeyValuePair<string, int> ImageWorkers { get; set; } = new("image", 5);
    public static KeyValuePair<string, int> RequestWorkers { get; set; } = new("request", 15);
    
    public static readonly ParallelOptions ParallelOptions = new()
    {
        MaxDegreeOfParallelism = (int)Math.Floor(Environment.ProcessorCount / 2.0),
    };

    public static string? AllowAdultContent { get; set; } = "false";
    
    public const int MaximumCardsInCarousel = 36;
    public const int MaximumItemsPerPage = 500;
    
    public const string TvMediaType = "tv";
    public const string MovieMediaType = "movie";
    public const string AnimeMediaType = "anime";
    public const string MusicMediaType = "music";
    public const string CollectionMediaType = "collection";
    public const string SpecialMediaType = "special";
}