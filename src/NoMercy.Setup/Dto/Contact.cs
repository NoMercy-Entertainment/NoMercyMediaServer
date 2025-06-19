using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class Contact
{
    [JsonProperty("homepage")] public string Homepage { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("email")] public string Email { get; set; } = string.Empty;
    [JsonProperty("dmca")] public string Dmca { get; set; } = string.Empty;
    [JsonProperty("languages")] public string Languages { get; set; } = string.Empty;
    [JsonProperty("socials")] public Socials Socials { get; set; } = new();
}