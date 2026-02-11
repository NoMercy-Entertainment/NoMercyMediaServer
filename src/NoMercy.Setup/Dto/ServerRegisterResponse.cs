using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
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