namespace NoMercy.NmSystem;

public class Config
{
    public static string AuthBaseUrl { get; set; } = "https://auth.nomercy.tv/realms/NoMercyTV/";
    public static string AuthBaseDevUrl { get; set; } = "https://auth-dev.nomercy.tv/realms/NoMercyTV/";
    public static string TokenClientSecret = "1lHWBazSTHfBpuIzjAI6xnNjmwUnryai";
    public static readonly string TokenClientId = "nomercy-server";

    public static string AppBaseUrl = "https://app.nomercy.tv/";
    public static string ApiBaseUrl = "https://api.nomercy.tv/";
    public static string ApiServerBaseUrl = $"{ApiBaseUrl}v1/server/";

    public static int InternalServerPort { get; set; } = 7626;
    public static int ExternalServerPort { get; set; } = 7626;
    
    public static bool Swagger { get; set; } = true;

    public static KeyValuePair<string, int> QueueWorkers { get; set; } = new("queue", 1);
    public static KeyValuePair<string, int> EncoderWorkers { get; set; } = new("encoder", 2);
    public static KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);
    public static KeyValuePair<string, int> DataWorkers { get; set; } = new("data", 10);
    public static KeyValuePair<string, int> ImageWorkers { get; set; } = new("image", 5);
    public static KeyValuePair<string, int> RequestWorkers { get; set; } = new("request", 15);

    public static readonly string AppDataPath =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static readonly string AppPath = Path.Combine(AppDataPath, "NoMercy_C#");

    public static readonly string ConfigPath = Path.Combine(AppPath, "config");
    public static readonly string TokenFile = Path.Combine(ConfigPath, "token.json");
    public static readonly string ConfigFile = Path.Combine(ConfigPath, "config.json");
    public static bool IsDev { get; set; }
}
