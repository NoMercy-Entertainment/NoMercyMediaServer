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
    ) => new(index, kind, language, title, comment, isDefault, isForced);

    private static TrackMetadata Db(
        int index = 0,
        string kind = "audio",
        string? language = null,
        string? title = null,
        bool isDefault = false,
        bool isForced = false
    ) => new(index, kind, language, title, isDefault, isForced);

    // ------------------------------------------------------------------
    // Rule 1: Title — source preserved unless DB has explicit non-empty value
    // ------------------------------------------------------------------

    [Fact]
    public void Title_DbEmpty_SourceWins()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(title: "English DTS-HD MA")],
            [Db(title: null)]
        );

        result.Should().ContainSingle();
        result[0].Title.Should().Be("English DTS-HD MA");
    }

    [Fact]
    public void Title_DbNonEmpty_DbWins()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(title: "English DTS-HD MA")],
            [Db(title: "English (Director's Cut)")]
        );

        result.Should().ContainSingle();
        result[0].Title.Should().Be("English (Director's Cut)");
    }

    // ------------------------------------------------------------------
    // Rule 2: Language — source always wins; DB never overwrites
    // ------------------------------------------------------------------

    [Fact]
    public void Language_SourceAlwaysWins_EvenWhenDbHasDifferentValue()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(language: "eng")],
            [Db(language: "und")]
        );

        result.Should().ContainSingle();
        result[0].Language.Should().Be("eng");
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
            [Src(title: "Main Audio", comment: "Ripped from Blu-ray disc 1")],
            [Db(title: null)]
        );

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Main Audio", "source title must survive when DB is null");
    }

    // ------------------------------------------------------------------
    // Rule 4: IsDefault — DB wins
    // ------------------------------------------------------------------

    [Fact]
    public void IsDefault_DbWins_RegardlessOfSource()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(isDefault: false)],
            [Db(isDefault: true)]
        );

        result.Should().ContainSingle();
        result[0].IsDefault.Should().BeTrue("DB IsDefault must override source value");
    }

    // ------------------------------------------------------------------
    // Rule 5: IsForced — DB wins
    // ------------------------------------------------------------------

    [Fact]
    public void IsForced_DbWins_RegardlessOfSource()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(isForced: false)],
            [Db(isForced: true)]
        );

        result.Should().ContainSingle();
        result[0].IsForced.Should().BeTrue("DB IsForced must override source value");
    }

    // ------------------------------------------------------------------
    // Edge case 7: Source-only track (no DB row) — pass through as-is
    // ------------------------------------------------------------------

    [Fact]
    public void SourceOnly_NoDbRow_EmittedWithSourceValues()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(index: 2, kind: "audio", language: "jpn", title: "Japanese 5.1", isDefault: true)],
            []
        );

        result.Should().ContainSingle();
        TrackMetadata track = result[0];
        track.OutputIndex.Should().Be(2);
        track.Kind.Should().Be("audio");
        track.Language.Should().Be("jpn");
        track.Title.Should().Be("Japanese 5.1");
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

        IReadOnlyList<TrackMetadata> result = _merger.Merge([], [dbSub]);

        result.Should().ContainSingle();
        result[0].Should().Be(dbSub, "DB-only track must pass through without modification");
    }

    // ------------------------------------------------------------------
    // Multiple tracks — ordering by OutputIndex preserved
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleTracksWithDifferentIndexes_ResultSortedByOutputIndex()
    {
        IReadOnlyList<TrackMetadata> result = _merger.Merge(
            [Src(index: 1, language: "fra"), Src(index: 0, language: "eng")],
            [Db(index: 0, language: "und"), Db(index: 1, language: "und")]
        );

        result.Should().HaveCount(2);
        result[0].OutputIndex.Should().Be(0);
        result[0].Language.Should().Be("eng", "source language wins at index 0");
        result[1].OutputIndex.Should().Be(1);
        result[1].Language.Should().Be("fra", "source language wins at index 1");
    }
}
