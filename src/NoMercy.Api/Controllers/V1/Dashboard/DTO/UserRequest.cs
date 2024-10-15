using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;
public record UserRequest
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("email")] public string Email { get; set; }
    [JsonProperty("manage")] public bool Manage { get; set; }
    [JsonProperty("owner")] public bool Owner { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("allowed")] public bool Allowed { get; set; }
    [JsonProperty("audio_transcoding")] public bool AudioTranscoding { get; set; }
    [JsonProperty("video_transcoding")] public bool VideoTranscoding { get; set; }
    [JsonProperty("no_transcoding")] public bool NoTranscoding { get; set; }
    [JsonProperty("libraries")] public Ulid[]? Libraries { get; set; }
}