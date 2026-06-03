using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;

/// <summary>
/// Response from <c>GET /ws/2/discid/{id}</c> (exact TOC match) or
/// <c>GET /ws/2/discid/-?toc=…</c> (fuzzy TOC lookup).
///
/// Exact match: <c>releases</c> is populated directly on the root object.
/// Fuzzy match: the server returns the same shape — an array of releases
/// whose TOC structure matches the supplied toc= parameter.
/// </summary>
public class DiscIdLookupResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("offset-count")]
    public int OffsetCount { get; set; }

    [JsonProperty("offsets")]
    public int[] Offsets { get; set; } = [];

    [JsonProperty("sectors")]
    public int Sectors { get; set; }

    [JsonProperty("releases")]
    public MusicBrainzReleaseAppends[] Releases { get; set; } = [];
}
