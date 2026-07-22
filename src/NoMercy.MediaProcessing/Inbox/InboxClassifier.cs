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

using System.Text.RegularExpressions;
using MovieFileLibrary;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Inbox;

public sealed partial class InboxClassifier
{
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".opus", ".wav", ".m4a"];

    private static readonly string[] VideoExtensions =
    [
        ".mp4",
        ".mkv",
        ".avi",
        ".webm",
        ".mov",
        ".m3u8",
    ];

    // SxxExx e.g. S01E01, S1E1
    private static readonly Regex SeasonEpisodePattern = new(
        pattern: @"[Ss]\d{1,2}[Ee]\d{1,2}",
        options: RegexOptions.Compiled
    );

    // NxNN e.g. 1x01, 2x12
    private static readonly Regex EpisodePrefixPattern = new(
        pattern: @"\b\d{1,2}x\d{2}\b",
        options: RegexOptions.Compiled
    );

    // "Season N" or "Season 01" in any path segment
    private static readonly Regex SeasonFolderPattern = new(
        pattern: @"\bSeason\s*\d+\b",
        options: RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // "Title (Year)" movie shape — year in parens
    private static readonly Regex MovieYearPattern = new(
        pattern: @"\((?:19|20)\d{2}\)",
        options: RegexOptions.Compiled
    );

    // Fansub bracket at start of FILENAME: "[Group] Title - NNN ..."
    // Conservative: only tag anime when the filename starts with a bracket
    // group AND has an absolute episode number as " - NNN " (1-4 digits, for
    // long-running shows like One Piece that exceed 999 episodes).
    private static readonly Regex FansubAbsoluteEpPattern = new(
        pattern: @"^\[[^\]]+\].*\s-\s\d{1,4}\s",
        options: RegexOptions.Compiled
    );

    [GeneratedRegex(pattern: @"\.NoMercy\.m3u8$", options: RegexOptions.IgnoreCase)]
    private static partial Regex FinishedHlsMasterPattern();

    [GeneratedRegex(pattern: @"^(video_\d+x\d+(_.+)?|audio_[A-Za-z0-9_]+)$")]
    private static partial Regex HlsLadderEntryPattern();

    private readonly IInboxMetadataProbe _probe;
    private readonly IInboxAudioTagReader _tagReader;

    public InboxClassifier(IInboxMetadataProbe probe, IInboxAudioTagReader tagReader)
    {
        _probe = probe;
        _tagReader = tagReader;
    }

    // -----------------------------------------------------------------------
    // Pure static helpers — unit-testable with no deps
    // -----------------------------------------------------------------------

    public static string MediaFamilyOf(string path)
    {
        string ext = Path.GetExtension(path: path).ToLowerInvariant();

        if (AudioExtensions.Contains(value: ext))
            return "music";

        if (VideoExtensions.Contains(value: ext))
            return "video";

        return "unknown";
    }

    public static string StructuralType(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path: path);

        if (FansubAbsoluteEpPattern.IsMatch(input: filename))
            return "anime";

        if (SeasonEpisodePattern.IsMatch(input: path))
            return "tv";

        if (EpisodePrefixPattern.IsMatch(input: path))
            return "tv";

        if (SeasonFolderPattern.IsMatch(input: path))
            return "tv";

        MovieDetector detector = new();
        MovieFile info = detector.GetInfo(filePath: path);

        if (info.IsSeries && (info.Season.HasValue || info.Episode.HasValue))
            return "tv";

        if (MovieYearPattern.IsMatch(input: path))
            return "movie";

        return "unknown";
    }

    public static bool IsFinishedHls(IEnumerable<string> siblingNames)
    {
        bool hasMaster = false;
        bool hasLadderEntry = false;

        foreach (string name in siblingNames)
        {
            if (FinishedHlsMasterPattern().IsMatch(input: name))
                hasMaster = true;

            if (HlsLadderEntryPattern().IsMatch(input: name))
                hasLadderEntry = true;
        }

        return hasMaster && hasLadderEntry;
    }

    // -----------------------------------------------------------------------
    // Full cascade
    // -----------------------------------------------------------------------

    public async Task<ClassificationResult> Classify(
        string path,
        Ulid driverId,
        CancellationToken ct
    )
    {
        string family = MediaFamilyOf(path: path);

        if (family == "music")
            return await ClassifyMusicAsync(path: path, driverId: driverId, ct: ct);

        if (family == "video")
            return await ClassifyVideoAsync(path: path, ct: ct);

        return new()
        {
            DetectedType = "unknown",
            Confidence = "low",
            Candidates = [],
        };
    }

    // -----------------------------------------------------------------------
    // Music branch
    // -----------------------------------------------------------------------

    private async Task<ClassificationResult> ClassifyMusicAsync(
        string path,
        Ulid driverId,
        CancellationToken ct
    )
    {
        InboxAudioTags? tags = await _tagReader.ReadAsync(path: path, driverId: driverId, ct: ct);

        if (tags is null)
        {
            return new()
            {
                DetectedType = "music",
                Confidence = "low",
                Candidates = [],
            };
        }

        if (tags.MusicBrainzReleaseId.HasValue && tags.MusicBrainzReleaseId.Value != Guid.Empty)
        {
            CandidateMatch? candidate = await _probe.LookupMusicReleaseAsync(
                releaseId: tags.MusicBrainzReleaseId.Value,
                ct: ct
            );

            if (candidate is not null)
            {
                return new()
                {
                    DetectedType = "music",
                    Confidence = "high",
                    Candidates = [candidate],
                };
            }
        }

        // Tags present but no resolvable release id
        return new()
        {
            DetectedType = "music",
            Confidence = "medium",
            Candidates = [],
        };
    }

    // -----------------------------------------------------------------------
    // Video branch
    // -----------------------------------------------------------------------

    private async Task<ClassificationResult> ClassifyVideoAsync(string path, CancellationToken ct)
    {
        string structuralType = StructuralType(path: path);

        // Parse title and year from path for provider search
        MovieDetector detector = new();
        MovieFile info = detector.GetInfo(filePath: path);
        string title = ExtractTitle(path: path, info: info);

        if (string.IsNullOrWhiteSpace(value: title))
        {
            return new()
            {
                DetectedType = structuralType == "unknown" ? "unknown" : structuralType,
                Confidence = "low",
                Candidates = [],
            };
        }

        int? year = ExtractYear(path: path, info: info);

        // Probe both movie and tv to handle ambiguous cases
        CandidateMatch[] movieHits = await _probe.SearchMoviesAsync(title: title, year: year, ct: ct);
        CandidateMatch[] tvHits = await _probe.SearchTvAsync(title: title, year: year, ct: ct);

        return FoldVideoResults(structuralType: structuralType, year: year, movieHits: movieHits, tvHits: tvHits);
    }

    private static ClassificationResult FoldVideoResults(
        string structuralType,
        int? year,
        CandidateMatch[] movieHits,
        CandidateMatch[] tvHits
    )
    {
        bool hasStrongMovieHit = HasStrongHit(hits: movieHits, queriedYear: year);
        bool hasStrongTvHit = HasStrongHit(hits: tvHits, queriedYear: year);

        // Conflicting strong signals → low regardless of structural type
        if (hasStrongMovieHit && hasStrongTvHit)
        {
            CandidateMatch[] combined = [.. movieHits.Take(count: 3), .. tvHits.Take(count: 3)];
            return new()
            {
                DetectedType = structuralType == "unknown" ? "unknown" : structuralType,
                Confidence = "low",
                Candidates = RankCandidates(candidates: combined),
            };
        }

        // Anime structural type: conservative — send to review unless we also
        // have strong TV signal, since anime sometimes matches TV on TMDB.
        if (structuralType == "anime")
        {
            if (hasStrongTvHit && !hasStrongMovieHit)
            {
                return new()
                {
                    DetectedType = "anime",
                    Confidence = "medium",
                    Candidates = RankCandidates(candidates: tvHits),
                };
            }

            return new()
            {
                DetectedType = "anime",
                Confidence = "low",
                Candidates = RankCandidates(candidates: [.. movieHits.Take(count: 3), .. tvHits.Take(count: 3)]),
            };
        }

        if (structuralType == "movie")
        {
            if (hasStrongMovieHit)
            {
                return new()
                {
                    DetectedType = "movie",
                    Confidence = "high",
                    Candidates = RankCandidates(candidates: movieHits),
                };
            }

            if (movieHits.Length > 0)
            {
                return new()
                {
                    DetectedType = "movie",
                    Confidence = "medium",
                    Candidates = RankCandidates(candidates: movieHits),
                };
            }

            return new()
            {
                DetectedType = "movie",
                Confidence = "low",
                Candidates = [],
            };
        }

        if (structuralType == "tv")
        {
            if (hasStrongTvHit)
            {
                return new()
                {
                    DetectedType = "tv",
                    Confidence = "high",
                    Candidates = RankCandidates(candidates: tvHits),
                };
            }

            if (tvHits.Length > 0)
            {
                return new()
                {
                    DetectedType = "tv",
                    Confidence = "medium",
                    Candidates = RankCandidates(candidates: tvHits),
                };
            }

            return new()
            {
                DetectedType = "tv",
                Confidence = "low",
                Candidates = [],
            };
        }

        // structuralType == "unknown"
        if (hasStrongMovieHit && !hasStrongTvHit)
        {
            return new()
            {
                DetectedType = "movie",
                Confidence = "medium",
                Candidates = RankCandidates(candidates: movieHits),
            };
        }

        if (hasStrongTvHit && !hasStrongMovieHit)
        {
            return new()
            {
                DetectedType = "tv",
                Confidence = "medium",
                Candidates = RankCandidates(candidates: tvHits),
            };
        }

        CandidateMatch[] allHits = [.. movieHits.Take(count: 3), .. tvHits.Take(count: 3)];
        return new()
        {
            DetectedType = "unknown",
            Confidence = "low",
            Candidates = RankCandidates(candidates: allHits),
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    // A "strong" hit: top result with score >= 0.5 AND year matches (or no
    // year was available to check against)
    private static bool HasStrongHit(CandidateMatch[] hits, int? queriedYear)
    {
        if (hits.Length == 0)
            return false;

        CandidateMatch top = hits[0];

        if (top.Score < 0.50)
            return false;

        if (
            queriedYear.HasValue
            && top.Year.HasValue
            && Math.Abs(value: top.Year.Value - queriedYear.Value) > 1
        )
            return false;

        return true;
    }

    private static CandidateMatch[] RankCandidates(CandidateMatch[] candidates)
    {
        return candidates.OrderByDescending(keySelector: c => c.Score).ToArray();
    }

    private static string ExtractTitle(string path, MovieFile info)
    {
        if (!string.IsNullOrWhiteSpace(value: info.Title))
            return info.Title;

        // Fall back: filename without extension, strip common suffixes
        string name = Path.GetFileNameWithoutExtension(path: path);

        // Strip resolution / quality tags
        name = Regex
            .Replace(
                input: name,
                pattern: @"\b(720p|1080p|2160p|4k|BluRay|WEB-DL|WEBRip|HDTV|x264|x265|HEVC|H\.?264|H\.?265|AAC|DTS|AC3)\b.*$",
                replacement: string.Empty,
                options: RegexOptions.IgnoreCase
            )
            .Trim();

        // Strip year in parens at end
        name = Regex.Replace(input: name, pattern: @"\s*\((?:19|20)\d{2}\)\s*$", replacement: string.Empty).Trim();

        return name.Trim(trimChars: ['.', ' ', '-', '_']);
    }

    private static int? ExtractYear(string path, MovieFile info)
    {
        // MovieFile.Year is string? in MovieFileLibrary
        if (!string.IsNullOrWhiteSpace(value: info.Year) && int.TryParse(s: info.Year, result: out int parsed))
            return parsed;

        Match yearMatch = MovieYearPattern.Match(input: path);
        if (!yearMatch.Success)
            return null;

        string yearStr = yearMatch.Value.Trim(trimChars: ['(', ')']);
        return int.TryParse(s: yearStr, result: out int parsedFromPath) ? parsedFromPath : null;
    }
}
