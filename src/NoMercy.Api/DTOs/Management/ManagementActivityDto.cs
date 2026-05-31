using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Management;

public record ManagementActivityDto
{
    [JsonProperty("active_streams")]
    public int ActiveStreams { get; init; }

    [JsonProperty("active_encodes")]
    public int ActiveEncodes { get; init; }

    [JsonProperty("can_interrupt_safely")]
    public bool CanInterruptSafely { get; init; }
}
