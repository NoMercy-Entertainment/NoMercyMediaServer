using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbLoginResponse : TvdbResponse<TvdbLogin>
{
}

public class TvdbLogin
{
    [JsonProperty("token")] public string Token { get; set; } = "";
    [JsonProperty("expiresAt")] public DateTime ExpiresAt = DateTime.Now.AddMonths(1);
}