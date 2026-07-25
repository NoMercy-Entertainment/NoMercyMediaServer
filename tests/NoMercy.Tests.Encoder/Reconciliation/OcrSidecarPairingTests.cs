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

using NoMercy.Encoder.Reconciliation;

namespace NoMercy.Tests.Encoder.Reconciliation;

/// <summary>
/// An OCR sidecar counts only when it is named as its bitmap track's sibling —
/// same <c>{lang}.{type}</c>, per <c>FileManager.SubtitleFileRegex</c>. That is
/// the same pairing the library scan uses to decide a bitmap subtitle is
/// orphaned, so a sidecar this reconciler counts is exactly one a player lists.
/// </summary>
public class OcrSidecarPairingTests
{
    private const string Stem = "Frieren.Beyond.Journey's.End.S01E01.The.Journey's.End.NoMercy";

    private static ExistingOutputEntry File(string name) => new($"subtitles/{name}", 4096);

    [Fact]
    public void PairedVttCountsAsOcred()
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            File($"{Stem}.eng.full.vtt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(1);
    }

    [Fact]
    public void InventedOcrNameDoesNotCount()
    {
        // eng.ocr0.vtt parses as type "ocr0", so it pairs with nothing and the
        // .mks stays orphaned — the file exists but no player ever lists it.
        List<ExistingOutputEntry> files = [File($"{Stem}.eng.full.mks"), File("eng.ocr0.vtt")];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(0);
    }

    [Fact]
    public void VttForADifferentVariantDoesNotPair()
    {
        // Two bitmap tracks, one OCR result: only the sign track is covered, so
        // the full track must still be reported as needing OCR.
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            File($"{Stem}.eng.sign.mks"),
            File($"{Stem}.eng.sign.vtt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(1);
    }

    [Fact]
    public void VttForADifferentLanguageDoesNotPair()
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            File($"{Stem}.jpn.full.vtt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(0);
    }

    [Fact]
    public void BothTracksPaired()
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            File($"{Stem}.eng.sign.mks"),
            File($"{Stem}.eng.full.vtt"),
            File($"{Stem}.eng.sign.vtt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(2);
    }

    [Theory]
    [InlineData("sup")]
    [InlineData("idx")]
    [InlineData("vob")]
    public void EveryBitmapContainerPairs(string extension)
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.{extension}"),
            File($"{Stem}.eng.full.vtt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(1);
    }

    [Fact]
    public void AnSrtPairsJustAsAVttDoes()
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            File($"{Stem}.eng.full.srt"),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(1);
    }

    [Fact]
    public void ZeroByteVttDoesNotPair()
    {
        List<ExistingOutputEntry> files =
        [
            File($"{Stem}.eng.full.mks"),
            new($"subtitles/{Stem}.eng.full.vtt", 0),
        ];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(0);
    }

    [Fact]
    public void TextOnlyBundleHasNothingToPair()
    {
        List<ExistingOutputEntry> files = [File($"{Stem}.eng.full.vtt")];

        EncodeReconciler.CountOcredBitmapSidecars(files).Should().Be(0);
    }
}
