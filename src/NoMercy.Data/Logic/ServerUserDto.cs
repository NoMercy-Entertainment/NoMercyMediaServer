using Newtonsoft.Json;

namespace NoMercy.Data.Logic;
public class ServerUserDto
{
    [JsonProperty("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; }= string.Empty;
    [JsonProperty("email")] public string Email { get; set; }= string.Empty;
    [JsonProperty("enabled")] public bool Enabled { get; set; }
    [JsonProperty("cache_id")] public string CacheId { get; set; }= string.Empty;
    [JsonProperty("avatar")] public Uri? Avatar { get; set; }
    [JsonProperty("is_owner")] public bool IsOwner { get; set; }
}