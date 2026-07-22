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

using NoMercy.Encoder.Metadata;

namespace NoMercy.Tests.Encoder.Metadata;

/// <summary>
/// Unit tests for MetadataMerger field-level precedence rules.
/// One test per rule (6), plus source-only and DB-only edge cases (2).
/// </summary>
public class MetadataMergerTests
{
    private readonly MetadataMerger _merger = new();

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static SourceTrackMetadata Src(
        int index = 0,
        string kind = "audio",
        string? language = null,
        string? title = null,
        string? comment = null,
        bool isDefault = false,
        bool isForced = false
    ) => new(OutputIndex: index, Kind: kind, Language: language, Title: title, Comment: comment, IsDefault: isDefault, IsForced: isForced);

    private static TrackMetadata Db(
        int index = 0,
        string kind = "audio",
        string? language = null,
        string? title = null,
        bool isDefault = false,
        bool isForced = false
    ) => new(OutputIndex: index, Kind: kind, Language: language, Title: title, IsDefault: isDefault, IsForced: isForced);

    // ------------------------------------------------------------------
    // Rule 1: Title — source preserved unless DB has explicit non-empty value
    // ------------------------------------------------------------------

    [Fact]
    public void Title_DbEmpty_SourceWins()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(title: "English DTS-HD MA")],
            dbTracks: [Db(title: null)]
        );

        result.Should().ContainSingle();
        result[index: 0].Title.Should().Be(expected: "English DTS-HD MA");
    }

    [Fact]
    public void Title_DbNonEmpty_DbWins()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(title: "English DTS-HD MA")],
            dbTracks: [Db(title: "English (Director's Cut)")]
        );

        result.Should().ContainSingle();
        result[index: 0].Title.Should().Be(expected: "English (Director's Cut)");
    }

    // ------------------------------------------------------------------
    // Rule 2: Language — source always wins; DB never overwrites
    // ------------------------------------------------------------------

    [Fact]
    public void Language_SourceAlwaysWins_EvenWhenDbHasDifferentValue()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(language: "eng")],
            dbTracks: [Db(language: "und")]
        );

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "eng");
    }

    // ------------------------------------------------------------------
    // Rule 3: Comment — source preserved; DB has no comment field,
    //         nothing to overwrite. The merger emits a TrackMetadata
    //         (which has no Comment field) — the caller may use
    //         SourceTrackMetadata.Comment directly for supplemental tagging.
    //         This test verifies the merger does not corrupt the merged
    //         title when a comment is present in source.
    // ------------------------------------------------------------------

    [Fact]
    public void Comment_SourcePresent_MergedTitleUnaffected()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(title: "Main Audio", comment: "Ripped from Blu-ray disc 1")],
            dbTracks: [Db(title: null)]
        );

        result.Should().ContainSingle();
        result[index: 0].Title.Should().Be(expected: "Main Audio", because: "source title must survive when DB is null");
    }

    // ------------------------------------------------------------------
    // Rule 4: IsDefault — DB wins
    // ------------------------------------------------------------------

    [Fact]
    public void IsDefault_DbWins_RegardlessOfSource()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(isDefault: false)],
            dbTracks: [Db(isDefault: true)]
        );

        result.Should().ContainSingle();
        result[index: 0].IsDefault.Should().BeTrue(because: "DB IsDefault must override source value");
    }

    // ------------------------------------------------------------------
    // Rule 5: IsForced — DB wins
    // ------------------------------------------------------------------

    [Fact]
    public void IsForced_DbWins_RegardlessOfSource()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(isForced: false)],
            dbTracks: [Db(isForced: true)]
        );

        result.Should().ContainSingle();
        result[index: 0].IsForced.Should().BeTrue(because: "DB IsForced must override source value");
    }

    // ------------------------------------------------------------------
    // Edge case 7: Source-only track (no DB row) — pass through as-is
    // ------------------------------------------------------------------

    [Fact]
    public void SourceOnly_NoDbRow_EmittedWithSourceValues()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(index: 2, kind: "audio", language: "jpn", title: "Japanese 5.1", isDefault: true)],
            dbTracks: []
        );

        result.Should().ContainSingle();
        TrackMetadata track = result[index: 0];
        track.OutputIndex.Should().Be(expected: 2);
        track.Kind.Should().Be(expected: "audio");
        track.Language.Should().Be(expected: "jpn");
        track.Title.Should().Be(expected: "Japanese 5.1");
        track.IsDefault.Should().BeTrue();
        track.IsForced.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Edge case 8: DB-only track (no source row, e.g. acquired subtitle)
    //             — emitted unchanged
    // ------------------------------------------------------------------

    [Fact]
    public void DbOnly_NoSourceRow_EmittedUnchanged()
    {
        TrackMetadata dbSub = Db(
            index: 5,
            kind: "subtitle",
            language: "eng",
            title: "SDH",
            isDefault: false,
            isForced: false
        );

        IReadOnlyList<TrackMetadata> result = _merger.Merge(source: [], dbTracks: [dbSub]);

        result.Should().ContainSingle();
        result[index: 0].Should().Be(expected: dbSub, because: "DB-only track must pass through without modification");
    }

    // ------------------------------------------------------------------
    // Multiple tracks — ordering by OutputIndex preserved
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleTracksWithDifferentIndexes_ResultSortedByOutputIndex()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            source: [Src(index: 1, language: "fra"), Src(index: 0, language: "eng")],
            dbTracks: [Db(index: 0, language: "und"), Db(index: 1, language: "und")]
        );

        result.Should().HaveCount(expected: 2);
        result[index: 0].OutputIndex.Should().Be(expected: 0);
        result[index: 0].Language.Should().Be(expected: "eng", because: "source language wins at index 0");
        result[index: 1].OutputIndex.Should().Be(expected: 1);
        result[index: 1].Language.Should().Be(expected: "fra", because: "source language wins at index 1");
    }
}
