using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

public class DeviceDropNotice : Timestamps
{
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }

    [JsonProperty("device_name")]
    public string DeviceName { get; set; } = "";

    [JsonProperty("reason")]
    public string Reason { get; set; } = ""; // "ttl" | "efuse" | "manual"

    [JsonProperty("acknowledged")]
    public bool Acknowledged { get; set; }
}
