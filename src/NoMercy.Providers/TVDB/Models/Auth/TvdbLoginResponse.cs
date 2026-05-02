using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Auth;

public class TvdbLoginResponse : TvdbResponse<TvdbLogin> { }

public class TvdbLogin
{
    [JsonProperty("token")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(1);
}
