namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// The media kind a disc resolves to. Distinct from <see cref="MediaType"/>
/// which exists on individual candidates; this is the disc-level kind
/// determined by the identifier.
/// </summary>
public enum MediaKind
{
    Movie,
    Tv,
    Music,
}

/// <summary>
/// Top-level result returned by <see cref="DiscIdentificationService"/>.
/// </summary>
public record DiscIdentification(
    MediaKind Kind,
    DiscCandidate[] Candidates,
    double TopConfidence,
    bool AutoApply,
    bool NeedsManualAssignment
)
{
    /// <summary>Highest-confidence candidate, or null when there are none.</summary>
    public DiscCandidate? TopCandidate => Candidates.Length > 0 ? Candidates[0] : null;
}

/// <summary>
/// A single ranked candidate returned as part of <see cref="DiscIdentification"/>.
/// </summary>
/// <param name="Source">Provider name, e.g. "tmdb" or "musicbrainz".</param>
/// <param name="StableId">
/// Provider-stable identifier: TMDB numeric id for video, MusicBrainz release
/// MBID (string form of Guid) for music.
/// </param>
/// <param name="Title">Human-readable release title.</param>
/// <param name="Year">Release year, or null when unknown.</param>
/// <param name="PosterUrl">Poster image URL, or null.</param>
/// <param name="BackdropUrl">Backdrop/fanart URL, or null.</param>
/// <param name="Confidence">0.0–1.0 confidence score.</param>
/// <param name="SeasonNumber">TV only — season number when resolved.</param>
/// <param name="EpisodeNumber">TV only — episode number when resolved.</param>
/// <param name="Type">
/// Media type of this candidate — Movie, TvShow, or Music. Null when the
/// identifier did not determine a per-candidate type.
/// </param>
/// <param name="TrackMapping">
/// Music only — per-track mapping from disc track index (1-based) to the
/// MusicBrainz recording MBID.
/// </param>
public record DiscCandidate(
    string Source,
    string StableId,
    string Title,
    int? Year,
    string? PosterUrl,
    string? BackdropUrl,
    double Confidence,
    MediaType? Type = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null,
    TrackMapping[]? TrackMapping = null
);

/// <summary>
/// Maps a single CD track to its MusicBrainz recording MBID.
/// </summary>
/// <param name="TrackIndex">1-based disc track number.</param>
/// <param name="RecordingMbid">MusicBrainz recording MBID.</param>
/// <param name="Title">Track title from MusicBrainz.</param>
/// <param name="ArtistCredit">Formatted artist credit string.</param>
/// <param name="DurationMs">Track duration in milliseconds as reported by MusicBrainz.</param>
public record TrackMapping(
    int TrackIndex,
    Guid RecordingMbid,
    string Title,
    string ArtistCredit,
    int? DurationMs
);
