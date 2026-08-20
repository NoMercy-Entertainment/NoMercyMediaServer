// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// What a playable disc playlist is, read entirely from its .mpls (no driving, no guessing): the
/// feature, a bonus video, or a non-playable index/transition playlist that must be excluded from the
/// menu. The disc rarely labels a playlist's role, so the role is inferred from structural signals the
/// .mpls states outright — playitem count, chapter marks, and whether it carries real audio/subtitle
/// streams — the same signals a human reading the disc layout would use.
public enum DiscContentKind
{
    /// The main movie: the longest single-clip playlist that carries real audio (and usually many
    /// chapter marks). There is exactly one.
    Feature,

    /// A bonus video — featurette, deleted scene, trailer, short. Real content (carries audio), shorter
    /// than the feature.
    Bonus,

    /// A non-playable playlist that is part of the disc's machinery, not its content: a scene/chapter
    /// INDEX (hundreds of playitems, no audio), or a transition/loader stub (a few seconds, no audio).
    /// These are excluded from the menu — exposing them would litter it with junk.
    Index,
}

/// One catalogued playlist: its id, what it is, its real duration, and the stream/chapter counts the
/// .mpls declares. The counts are carried so the consumer can wire SCENES (chapter marks) and SETTINGS
/// (audio/subtitle tracks) from the disc's own truth rather than re-deriving them by driving.
public sealed record DiscContentTitle
{
    public required int Playlist { get; init; }
    public required DiscContentKind Kind { get; init; }
    public required double DurationSeconds { get; init; }
    public required int PlayItemCount { get; init; }
    public required int ChapterCount { get; init; }
    public required int AudioCount { get; init; }
    public required int SubtitleCount { get; init; }

    /// The presentation-second of each chapter (entry) mark, in order — the disc's real scene boundaries.
    /// These drive the feature reel's marks (chapter-aware scrubber) and the SCENES grid's seek targets,
    /// so a scene button jumps to the disc's own chapter point instead of doing nothing.
    public IReadOnlyList<double> ChapterTimes { get; init; } = [];

    /// ISO-639-2 language of each audio stream, in the .mpls STN order — the HLS alternate-audio
    /// renditions and the setup menu's audio rows.
    public IReadOnlyList<string> AudioLanguages { get; init; } = [];

    /// ISO-639-2 language of each PG subtitle stream, in the .mpls STN order — the subtitle tracks the
    /// player offers (cue extraction keys off these indices).
    public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];
}

/// Builds the disc's playable-content catalog from its parsed playlists. The feature and bonus videos
/// are recovered structurally — never by playing the disc — so the catalog is the authoritative truth
/// about WHAT each playlist is; reachability (which menu button launches which) is a separate concern.
public static class DiscContentCatalog
{
    // PlayListMark type 1 = entry (a chapter point the user can jump to); type 2 = link (an internal
    // playback jump, not a scene). Only entry marks are chapters.
    private const int EntryMarkType = 1;

    // A playlist with this many playitems is a scene/chapter INDEX (every chapter is its own item), not
    // a single playable title. Frostbite's index playlists carry 300+ items; its content carries 1-4.
    private const int IndexPlayItemThreshold = 50;

    // Below this, a playlist with no audio is a transition/loader stub (logos, menu wipes), not content.
    private const double StubCeilingSeconds = 30;

    // The feature must be at least this long — guards against picking a long index playlist (which has
    // no audio anyway) or a trailer reel as the main movie.
    private const double FeatureFloorSeconds = 20 * 60;

    public static IReadOnlyList<DiscContentTitle> Build(
        IReadOnlyDictionary<int, MplsPlaylist> playlistsById
    )
    {
        List<DiscContentTitle> playable = [];
        foreach ((int playlist, MplsPlaylist mpls) in playlistsById)
        {
            if (IsIndexOrStub(mpls))
            {
                continue;
            }

            // Entry marks (type 1) are the disc's chapter points; link marks (type 2) are internal jumps,
            // not scenes. Carry the entry seconds in order so scene selection seeks to real chapters.
            List<double> chapterTimes =
            [
                .. mpls
                    .Marks.Where(mark => mark.MarkType == EntryMarkType)
                    .Select(mark => mark.Seconds),
            ];

            playable.Add(
                new DiscContentTitle
                {
                    Playlist = playlist,
                    Kind = DiscContentKind.Bonus,
                    DurationSeconds = mpls.TotalSeconds,
                    PlayItemCount = mpls.PlayItems.Count,
                    ChapterCount = chapterTimes.Count,
                    AudioCount = mpls.AudioStreams.Count,
                    SubtitleCount = mpls.SubtitleStreams.Count,
                    ChapterTimes = chapterTimes,
                    AudioLanguages = [.. mpls.AudioStreams.Select(s => s.Language ?? "und")],
                    SubtitleLanguages = [.. mpls.SubtitleStreams.Select(s => s.Language ?? "und")],
                }
            );
        }

        return PromoteFeature(playable);
    }

    // A playlist that carries no audio is never user-facing content: either a scene/chapter index
    // (hundreds of items) or a silent transition/loader. A playlist with hundreds of playitems is an
    // index even if it somehow carries audio. Everything else with audio is real content.
    private static bool IsIndexOrStub(MplsPlaylist mpls)
    {
        if (mpls.PlayItems.Count >= IndexPlayItemThreshold)
        {
            return true;
        }

        if (mpls.AudioStreams.Count == 0)
        {
            return true;
        }

        return mpls.TotalSeconds < StubCeilingSeconds;
    }

    // The feature is the longest remaining playable title (provided it clears the feature floor). Exactly
    // one playlist becomes Feature; the rest stay Bonus. Sorted longest-first so the catalog reads in a
    // stable, meaningful order.
    private static IReadOnlyList<DiscContentTitle> PromoteFeature(List<DiscContentTitle> playable)
    {
        List<DiscContentTitle> sorted = playable
            .OrderByDescending(title => title.DurationSeconds)
            .ToList();

        if (sorted.Count > 0 && sorted[0].DurationSeconds >= FeatureFloorSeconds)
        {
            sorted[0] = sorted[0] with { Kind = DiscContentKind.Feature };
        }

        return sorted;
    }
}
