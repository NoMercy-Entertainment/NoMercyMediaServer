using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Streaming.Dtos;

public record StartLiveSessionResponse(
    [property: JsonProperty("session_id")] string SessionId,
    [property: JsonProperty("playlist_url")] string PlaylistUrl,
    [property: JsonProperty("quality_id")] string QualityId,
    [property: JsonProperty("quality_label")] string QualityLabel
);

public record ReportPositionRequest([property: JsonProperty("time_seconds")] double TimeSeconds);
