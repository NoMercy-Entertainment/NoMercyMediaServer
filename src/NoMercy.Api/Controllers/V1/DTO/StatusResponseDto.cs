using Newtonsoft.Json;
using NoMercy.NmSystem;

namespace NoMercy.Api.Controllers.V1.DTO;
public record StatusResponseDto<T>
{
    private string? _message = "NoMercy is running!";
    [JsonProperty("status")] public string Status { get; set; } = "ok";
    [JsonProperty("data")] public T Data { get; set; } = default!;

    [JsonProperty("message")]
    public string? Message
    {
        get => _message;
        set => _message = value?.Localize();
    }

    [JsonProperty("args")] public dynamic[]? Args { get; set; } = [];
}
