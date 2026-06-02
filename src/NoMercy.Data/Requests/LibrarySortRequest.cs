using Newtonsoft.Json;

namespace NoMercy.Data.Requests;

public class LibrarySortRequest
{
    [JsonProperty("libraries")]
    public LibrarySortRequestItem[] Libraries { get; set; } = [];
}

public class LibrarySortRequestItem
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("order")]
    public int Order { get; set; }
}
