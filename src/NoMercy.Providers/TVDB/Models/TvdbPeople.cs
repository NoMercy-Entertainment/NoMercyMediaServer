using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbPeople
{
    
}

public class TvdbPeopleTypeResponse: TvdbResponse<TvdbPersonType[]>
{
}
public class TvdbPersonType
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}