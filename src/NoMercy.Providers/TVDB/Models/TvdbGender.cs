using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbGendersResponse : TvdbResponse<TvdbGender[]>
{
}

public class TvdbGender
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}

public enum TvdbGenderEnum
{
    Male,
    Female,
    NonBinary
}