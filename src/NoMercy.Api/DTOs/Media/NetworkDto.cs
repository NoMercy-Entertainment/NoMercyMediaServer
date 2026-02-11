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
using NoMercy.Providers.TMDB.Models.Networks;

namespace NoMercy.Api.DTOs.Media;

public class NetworkDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("headquarters")] public string? Headquarters { get; set; }
    [JsonProperty("link")] public Uri? Homepage { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("origin_country")] public string? OriginCountry { get; set; } 
    
    public NetworkDto(NetworkTv ntv)
    {
        Id = ntv.Network.Id;
        Name = ntv.Network.Name;
        Description = ntv.Network.Description;
        Headquarters = ntv.Network.Headquarters;
        Homepage = ntv.Network.Homepage;
        Logo = ntv.Network.Logo;
        OriginCountry = ntv.Network.OriginCountry;
    }

    public NetworkDto(TmdbNetwork ntv)
    {
        Id = ntv.Id;
        Name = ntv.Name;
        Logo = ntv.LogoPath;
        OriginCountry = ntv.OriginCountry;
    }
}