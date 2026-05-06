using NoMercy.Encoder.Naming;

namespace NoMercy.Encoder.Metadata;

/// <summary>
/// Builds FFmpeg -metadata / -metadata:s:* / -disposition:* / -attach args
/// from DB metadata rows. Pure function — no DI dependencies.
/// </summary>
public interface IMetadataInjector
{
    IReadOnlyList<string> BuildArgs(MetadataInjectionContext ctx);
}

// ---------------------------------------------------------------------------
// Context + supporting records
// ---------------------------------------------------------------------------

/// <summary>
/// All information needed to emit metadata args for one encode command.
/// </summary>
public record MetadataInjectionContext(
    MediaItemRef Media,
    IReadOnlyList<TrackMetadata> Tracks,
    IReadOnlyList<string> AttachmentPaths
);

/// <summary>Per-stream metadata supplied from the DB row.</summary>
public record TrackMetadata(
    int OutputIndex,
    /// <summary>"video" | "audio" | "subtitle"</summary>
    string Kind,
    string? Language,
    string? Title,
    bool IsDefault,
    bool IsForced
);

// ---------------------------------------------------------------------------
// MediaItemRef hierarchy — base lives in Naming; episode variant adds episode
// fields so the injector never has to reach into a DB context itself.
// ---------------------------------------------------------------------------

/// <summary>Movie-specific media reference with optional description.</summary>
public record MovieMediaRef(
    MediaType Type,
    long Id,
    string Title,
    int? Year,
    string? Description = null
) : MediaItemRef(Type, Id, Title, Year);

/// <summary>Episode-specific media reference carrying show/season/episode fields.</summary>
public record EpisodeMediaRef(
    MediaType Type,
    long Id,
    string Title,
    int? Year,
    string ShowTitle,
    int SeasonNumber,
    int EpisodeNumber,
    string? Description = null
) : MediaItemRef(Type, Id, Title, Year);
