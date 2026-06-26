namespace NoMercy.NmSystem.Configuration;

public class ExternalServicesConfig
{
    public string AuthBaseUrl { get; set; } = "https://auth.nomercy.tv/realms/NoMercyTV/";
    public string AppBaseUrl { get; set; } = "https://app.nomercy.tv/";
    public string ApiBaseUrl { get; set; } = "https://api.nomercy.tv/";
    public string TokenClientId { get; set; } = "nomercy-server";
    public string DnsServer { get; set; } = "1.1.1.1";
}
