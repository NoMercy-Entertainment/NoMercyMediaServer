using Newtonsoft.Json;

namespace NoMercy.MediaProcessing.Inbox;

public sealed record InboxAudioTags
{
    [JsonProperty("music_brainz_release_id")]
    public Guid? MusicBrainzReleaseId { get; init; }

    [JsonProperty("album")]
    public string? Album { get; init; }

    [JsonProperty("artist")]
    public string? Artist { get; init; }
}
