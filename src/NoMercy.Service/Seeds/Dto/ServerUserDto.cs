using Newtonsoft.Json;

namespace NoMercy.Service.Seeds.Dto;

public class ServerUserDto
{
    [JsonProperty("data")] public ServerUserDtoData[] Data { get; set; } = [];
}

public class ServerUserDtoData
{
    [JsonProperty("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("email")] public string Email { get; set; } = string.Empty;
    [JsonProperty("enabled")] public bool Enabled { get; set; } = true;
    [JsonProperty("avatar")] public Uri? Avatar { get; set; }
    [JsonProperty("is_owner")] public bool IsOwner { get; set; }
}