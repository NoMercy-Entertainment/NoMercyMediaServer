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

using FluentAssertions;
using NoMercy.MediaProcessing.Files;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// Every release id below came from a live AcoustID lookup of three tracks in
/// "Blondie - Greatest Hits (2002)" on 2026-08-02. Those three tracks alone named 324
/// distinct releases between them: fetching each one costs a rate-limited MusicBrainz
/// call and then an ffprobe of the whole folder to score it, so the folder has to agree
/// on a shortlist before any of that happens.
/// </summary>
[Trait("Category", "Unit")]
public class FileRepositoryRankCandidatesTests
{
    // The three releases all three sampled tracks named, all 20 tracks long — every one
    // of them a pressing of "Atomic: The Very Best of Blondie".
    private static readonly Guid AtomicFr = Guid.Parse("3fa7841a-81d5-4681-a1e4-838445c3a01c");
    private static readonly Guid AtomicAu = Guid.Parse("224f40e5-a693-482c-aac3-8041adad5760");
    private static readonly Guid AtomicJp = Guid.Parse("3b0c4ec5-a065-31ac-a006-50e8072f3de5");

    // A single-track release carrying one of the same recordings: named by one track,
    // and the wrong length to be this folder.
    private static readonly Guid UnrelatedSingle = Guid.Parse(
        "7047d8d5-e91c-4d48-90cc-eba5d6dc96ea"
    );

    private const int FolderTrackCount = 20;

    [Fact]
    public void A_release_that_cannot_be_this_folder_is_never_fetched()
    {
        List<FileRepository.ReleaseCandidate> candidates =
        [
            new(AtomicFr, FolderTrackCount),
            new(UnrelatedSingle, 2),
        ];

        FileRepository.RankCandidates(candidates, FolderTrackCount).Should().Equal(AtomicFr);
    }

    [Fact]
    public void The_release_most_of_the_folder_agrees_on_comes_first()
    {
        List<FileRepository.ReleaseCandidate> candidates =
        [
            new(AtomicAu, FolderTrackCount),
            new(AtomicFr, FolderTrackCount),
            new(AtomicFr, FolderTrackCount),
            new(AtomicFr, FolderTrackCount),
            new(AtomicAu, FolderTrackCount),
            new(AtomicJp, FolderTrackCount),
        ];

        FileRepository
            .RankCandidates(candidates, FolderTrackCount)
            .Should()
            .Equal(AtomicFr, AtomicAu, AtomicJp);
    }

    /// <summary>
    /// A folder missing a track, or one whose releases carry no track count, still has to
    /// return something to triage — "no results" is the failure this whole path exists to
    /// stop, so an unmatchable count falls back to agreement rather than to nothing.
    /// </summary>
    [Fact]
    public void No_release_matching_the_file_count_falls_back_instead_of_returning_nothing()
    {
        List<FileRepository.ReleaseCandidate> candidates =
        [
            new(AtomicFr, 19),
            new(AtomicFr, 19),
            new(UnrelatedSingle, null),
        ];

        FileRepository
            .RankCandidates(candidates, FolderTrackCount)
            .Should()
            .Equal(AtomicFr, UnrelatedSingle);
    }

    [Fact]
    public void The_shortlist_stays_short_enough_to_fetch()
    {
        List<FileRepository.ReleaseCandidate> candidates = Enumerable
            .Range(0, 324)
            .Select(index => new FileRepository.ReleaseCandidate(
                Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
                FolderTrackCount
            ))
            .ToList();

        FileRepository.RankCandidates(candidates, FolderTrackCount).Should().HaveCount(10);
    }

    [Fact]
    public void No_candidates_yields_no_lookups()
    {
        FileRepository.RankCandidates([], FolderTrackCount).Should().BeEmpty();
    }
}
