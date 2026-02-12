using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Setup.Dto;

public class ServerRegisterResponse
{
    [JsonProperty("data")] public ServerRegisterResponseData Data { get; set; } = new();
}

public class ServerRegisterResponseData
{
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
    [JsonProperty("id")] public string ServerId { get; set; } = string.Empty;
    [JsonProperty("user")] public User User { get; set; } = new();
}