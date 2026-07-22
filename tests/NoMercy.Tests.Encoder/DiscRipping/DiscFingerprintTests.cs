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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class DiscFingerprintTests
{
    [Fact]
    public void Compute_EmptyDisc_ReturnsEmptyString()
    {
        DiscInfo info = new(Type: OpticalDiscType.BluRay, DiscLabel: null, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        DiscFingerprint.Compute(info: info).Should().BeEmpty();
    }

    [Fact]
    public void Compute_SameStructureSameHash()
    {
        DiscInfo a = MakeDisc(titles: [(1, 600), (2, 1200)]);
        DiscInfo b = MakeDisc(titles: [(1, 600), (2, 1200)]);
        DiscFingerprint.Compute(info: a).Should().Be(expected: DiscFingerprint.Compute(info: b));
    }

    [Fact]
    public void Compute_TitleOrderDoesNotMatter()
    {
        DiscInfo a = MakeDisc(titles: [(1, 600), (2, 1200)]);
        DiscInfo b = MakeDisc(titles: [(2, 1200), (1, 600)]);
        DiscFingerprint.Compute(info: a).Should().Be(expected: DiscFingerprint.Compute(info: b));
    }

    [Fact]
    public void Compute_DifferentDurationsDifferentHash()
    {
        DiscInfo a = MakeDisc(titles: (1, 600));
        DiscInfo b = MakeDisc(titles: (1, 601));
        DiscFingerprint.Compute(info: a).Should().NotBe(unexpected: DiscFingerprint.Compute(info: b));
    }

    [Fact]
    public void Compute_DifferentIndicesDifferentHash()
    {
        DiscInfo a = MakeDisc(titles: (1, 600));
        DiscInfo b = MakeDisc(titles: (2, 600));
        DiscFingerprint.Compute(info: a).Should().NotBe(unexpected: DiscFingerprint.Compute(info: b));
    }

    [Fact]
    public void Compute_AvatarS1D1Shape_StableAcrossReprobes()
    {
        // The 19 Avatar S1D1 playlists (indices + duration in seconds)
        DiscInfo first = MakeDisc(titles: [(1601, 1419), (252, 239), (1610, 1300), (1617, 1356), (1618, 1341), (1619, 1366), (1620, 1384), (1621, 1387), (1622, 1344), (600, 11232), (250, 278), (602, 1348), (603, 1405), (604, 1389), (605, 1414), (606, 1432), (607, 1435), (608, 1392), (601, 1420)]
        );
        DiscInfo second = MakeDisc(titles: [(601, 1420), (608, 1392), (607, 1435), (606, 1432), (605, 1414), (604, 1389), (603, 1405), (602, 1348), (250, 278), (600, 11232), (1622, 1344), (1621, 1387), (1620, 1384), (1619, 1366), (1618, 1341), (1617, 1356), (1610, 1300), (252, 239), (1601, 1419)]
        );

        // Insert order shouldn't affect the hash.
        DiscFingerprint.Compute(info: first).Should().Be(expected: DiscFingerprint.Compute(info: second));
    }

    private static DiscInfo MakeDisc(params (int Index, int DurationSeconds)[] titles)
    {
        DiscTitle[] discTitles = titles
            .Select(selector: t => new DiscTitle(
                Index: t.Index,
                Name: $"Playlist {t.Index:D5}",
                Duration: TimeSpan.FromSeconds(seconds: t.DurationSeconds),
                VideoStreams: [],
                AudioStreams: [],
                Subtitles: [],
                Chapters: [],
                EstimatedSizeBytes: 0,
                IsMainFeature: false
            ))
            .ToArray();

        return new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: "Test Disc",
            Titles: discTitles,
            AudioTracks: null,
            TotalDuration: TimeSpan.FromSeconds(seconds: titles.Sum(selector: t => t.DurationSeconds))
        );
    }
}
