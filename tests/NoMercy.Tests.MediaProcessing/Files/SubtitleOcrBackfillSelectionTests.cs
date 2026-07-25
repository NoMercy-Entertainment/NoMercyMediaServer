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

using NoMercy.MediaProcessing.Files;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// Which preserved bitmap sidecars a scan queues for OCR backfill. The scan
/// must OCR only a bitmap track that has no text sibling: a title encoded before
/// the OCR pipeline worked keeps its <c>.sup</c> but never got a <c>.vtt</c>, so
/// clients that cannot render PGS show nothing — while a bitmap that already has
/// an <c>.ass</c>/<c>.srt</c>/<c>.vtt</c> of the same lang+variant must be left
/// alone or the picker shows the track twice.
/// </summary>
public class SubtitleOcrBackfillSelectionTests
{
    private const string Stem =
        "KanColle.Kantai.Collection.S01E02.Don't.Be.Bad,.Don't.Be.Ashamed,.Don't.Slack.NoMercy";

    [Fact]
    public void Bitmap_with_a_same_variant_text_sibling_is_left_alone()
    {
        // The real on-disk KanColle S01E02 case: eng.full has both an .ass and a
        // .sup (covered), eng.sign has only a .sup (orphaned).
        IReadOnlyList<OrphanedBitmapSubtitle> orphans = FileManager.SelectOrphanedBitmapSubtitles([
            $"{Stem}.eng.full.ass",
            $"{Stem}.eng.full.sup",
            $"{Stem}.eng.sign.sup",
        ]);

        orphans.Should().ContainSingle("only the textless bitmap track needs OCR");
        OrphanedBitmapSubtitle orphan = orphans[0];
        orphan.Language.Should().Be("eng");
        orphan.Variant.Should().Be("sign");
        orphan.SupName.Should().Be($"{Stem}.eng.sign.sup");
        orphan
            .MediaTitle.Should()
            .Be(Stem, "the OCR sidecar stem must match the .sup so the scan pairs them");
    }

    [Fact]
    public void Bitmap_with_no_text_sibling_is_selected()
    {
        IReadOnlyList<OrphanedBitmapSubtitle> orphans = FileManager.SelectOrphanedBitmapSubtitles([
            $"{Stem}.dut.full.sup",
        ]);

        orphans.Should().ContainSingle();
        orphans[0].Language.Should().Be("dut");
        orphans[0].Variant.Should().Be("full");
    }

    [Fact]
    public void Bitmap_already_covered_by_a_vtt_is_not_reprocessed()
    {
        // Idempotency at the selection layer: once OCR has produced the .vtt, a
        // later scan must not queue the same track again.
        IReadOnlyList<OrphanedBitmapSubtitle> orphans = FileManager.SelectOrphanedBitmapSubtitles([
            $"{Stem}.eng.sign.sup",
            $"{Stem}.eng.sign.vtt",
        ]);

        orphans.Should().BeEmpty();
    }

    [Fact]
    public void Non_subtitle_and_text_only_files_yield_nothing()
    {
        IReadOnlyList<OrphanedBitmapSubtitle> orphans = FileManager.SelectOrphanedBitmapSubtitles([
            $"{Stem}.eng.full.ass",
            $"{Stem}.eng.full.vtt",
            "chapters.vtt",
            "fonts.json",
        ]);

        orphans.Should().BeEmpty();
    }
}
