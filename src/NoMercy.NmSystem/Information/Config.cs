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

    public static string UserAgent =>
        $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    public static bool Started { get; set; }
    public static string? CloudflareTunnelToken { get; set; }

    public static NatStatus NatStatus { get; set; } = NatStatus.None;
    public static bool PortForwarded { get; set; }

    public static string? StunPublicIp { get; set; }
    public static int? StunPublicPort { get; set; }
    public static int StunPort => InternalServerPort + 1;

    private static int? _internalServerPort;

    public static int InternalServerPort
    {
        get => _internalServerPort ?? 7626;
        set => _internalServerPort = value;
    }

    private static int? _externalServerPort;

    public static int ExternalServerPort
    {
        get => _externalServerPort ?? 7626;
        set => _externalServerPort = value;
    }

    private static string? _managementPipeName;

    public static string ManagementPipeName
    {
        get => _managementPipeName ?? "NoMercyManagement";
        set => _managementPipeName = value;
    }

    public static string ManagementSocketPath =>
        Path.Combine(AppFiles.AppPath, "nomercy-management.sock");

    public static bool Swagger { get; set; } = true;

    public static bool IsDev { get; set; }
    public static bool IsTest { get; set; }
    public static bool UpdateAvailable { get; set; }
    public static bool RestartNeeded { get; set; }
    public static string? LatestVersion { get; set; }

    public static KeyValuePair<string, int> LibraryWorkers { get; set; } = new("library", 1);
    public static KeyValuePair<string, int> ImportWorkers { get; set; } = new("import", 2);
    public static KeyValuePair<string, int> ExtrasWorkers { get; set; } = new("extras", 4);
    public static KeyValuePair<string, int> EncoderWorkers { get; set; } = new("encoder", 1);

    // encoder-task is superseded by encoder-gpu + encoder-cpu (Task 2 resource scheduler).
    // Kept for backward compatibility with any persisted queue-state that still references it.
    public static KeyValuePair<string, int> EncoderTaskWorkers { get; set; } =
        new("encoder-task", 0);

    // Queue concurrency is the upper bound on how many bundles the queue can
    // *attempt* to run in parallel. The actual concurrency cap is enforced by
    // ResourceBudget's per-GPU and CPU semaphores at job pick-up time:
    // VideoEncodeJob.ResolveBundleResources sums the real NVENC slot + CPU
    // thread demand of every task in the bundle, so a bundle that uses all 8
    // NVENC sessions claims 8 slots — and another bundle has to wait until a
    // slot frees. Having multiple workers lets light bundles (audio, subs,
    // thumbs) ride along when there's spare budget. Sequential vs parallel is
    // a coordinator + semaphore decision, not a queue-worker count decision.
    public static KeyValuePair<string, int> GpuEncoderWorkers { get; set; } =
        new("encoder-gpu", Math.Min(4, Math.Max(1, Environment.ProcessorCount / 4)));

    public static KeyValuePair<string, int> CpuEncoderWorkers { get; set; } =
        new("encoder-cpu", Math.Max(1, Environment.ProcessorCount / 2));

    public static KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);
    public static KeyValuePair<string, int> ImageWorkers { get; set; } = new("image", 3);
    public static KeyValuePair<string, int> FileWorkers { get; set; } = new("file", 2);
    public static KeyValuePair<string, int> MusicWorkers { get; set; } = new("music", 2);

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
