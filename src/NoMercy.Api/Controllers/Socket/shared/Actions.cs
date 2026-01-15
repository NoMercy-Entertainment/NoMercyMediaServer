using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.Socket.shared;

public class Actions
{
    [JsonProperty("disallows")] public Disallows Disallows { get; set; } = null!;
}

public class Disallows
{
    [JsonProperty("previous")]
    public bool Previous { get; set; }
    
    [JsonProperty("next")]
    public bool Next { get; set; }

    [JsonProperty("resuming")]
    public bool Resuming { get; set; }

    [JsonProperty("pausing")]
    public bool Pausing { get; set; }

    [JsonProperty("toggling_repeat_context")]
    public bool TogglingRepeatContext { get; set; }

    [JsonProperty("toggling_repeat_track")]
    public bool TogglingRepeatTrack { get; set; }

    [JsonProperty("toggling_shuffle")]
    public bool TogglingShuffle { get; set; }

    [JsonProperty("seeking")]
    public bool Seeking { get; set; }
    
    [JsonProperty("stopping")]
    public bool Stopping { get; set; }
    
    [JsonProperty("muting")]
    public bool Muting { get; set; }
}