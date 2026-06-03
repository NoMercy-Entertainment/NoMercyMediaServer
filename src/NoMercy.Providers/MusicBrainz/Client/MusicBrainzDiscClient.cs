using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.Providers.MusicBrainz.Client;

/// <summary>
/// MusicBrainz disc-lookup operations.
///
/// Exact lookup:  GET /ws/2/discid/{id}?inc=recordings+artist-credits+release-groups&amp;fmt=json
/// Fuzzy lookup:  GET /ws/2/discid/-?toc=…&amp;inc=recordings+artist-credits+release-groups&amp;fmt=json
///
/// The caller is responsible for building the <c>toc=</c> string (see
/// <c>AudioCdIdentifier.BuildTocString</c> in NoMercy.OpticalMedia). This
/// keeps NoMercy.Providers free of a circular dependency on NoMercy.OpticalMedia.
///
/// Both calls use the base client's rate-limited queue (1 req/s) and
/// file-backed response cache.
/// </summary>
public sealed class MusicBrainzDiscClient : MusicBrainzBaseClient
{
    private static readonly string[] DefaultIncludes =
    [
        "recordings",
        "artist-credits",
        "release-groups",
    ];

    public MusicBrainzDiscClient()
        : base() { }

    /// <summary>
    /// Exact disc-id lookup. Returns null when the disc id is not found (404).
    /// </summary>
    public Task<DiscIdLookupResponse?> LookupByDiscId(
        string discId,
        bool? priority = false,
        CancellationToken ct = default
    )
    {
        Dictionary<string, string> queryParams = new()
        {
            ["inc"] = string.Join("+", DefaultIncludes),
            ["fmt"] = "json",
        };

        return Get<DiscIdLookupResponse>($"discid/{discId}", queryParams, priority);
    }

    /// <summary>
    /// Fuzzy TOC lookup using a pre-built <c>toc=</c> query string.
    /// The string format is: <c>firstTrack+lastTrack+leadOut+t1+t2…</c>
    /// where all offsets include the +150 pre-gap (as per the MusicBrainz spec).
    /// Build this string via <c>AudioCdIdentifier.BuildTocString(DiscToc)</c>.
    /// Returns null when the server returns no matches.
    /// </summary>
    public Task<DiscIdLookupResponse?> LookupByTocString(
        string tocString,
        bool? priority = false,
        CancellationToken ct = default
    )
    {
        Dictionary<string, string> queryParams = new()
        {
            ["toc"] = tocString,
            ["inc"] = string.Join("+", DefaultIncludes),
            ["fmt"] = "json",
        };

        return Get<DiscIdLookupResponse>("discid/-", queryParams, priority);
    }
}
