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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Corpus for <see cref="StringExtensions.CleanReleaseTitle"/>. Two invariants are
/// exercised exhaustively: (1) scene quality/source/codec/audio/HDR/flag tags are
/// removed so a clean title reaches the metadata lookup, and (2) real title words
/// that merely look like a tag substring are NEVER over-stripped.
/// </summary>
public class CleanReleaseTitleTests
{
    [Theory]
    // resolution
    [InlineData(data: ["Breaking.Bad.1080p.WEB-DL.x265-GROUP", "Breaking Bad"])]
    [InlineData(data: ["Some.Show.720p.HDTV.x264-LOL", "Some Show"])]
    [InlineData(data: ["Movie Name 2160p UHD BluRay REMUX HDR10 TrueHD Atmos-GRP", "Movie Name"])]
    [InlineData(data: ["Daredevil 1080i", "Daredevil"])]
    [InlineData(data: ["Blade Runner 2049 4K", "Blade Runner 2049"])]
    // source
    [InlineData(data: ["The.Title.WEBRip.DDP5.1.x264", "The Title"])]
    [InlineData(data: ["Show Name WEB-DL", "Show Name"])]
    [InlineData(data: ["Cool.Show.BluRay", "Cool Show"])]
    [InlineData(data: ["Some Movie DVDRip XviD", "Some Movie"])]
    [InlineData(data: ["Other Film HDTV", "Other Film"])]
    // codec / audio / hdr
    [InlineData(data: ["Film.H.264.AAC", "Film"])]
    [InlineData(data: ["Title.HEVC.10bit", "Title"])]
    [InlineData(data: ["A.B.C.HDR.x265", "A B C"])]
    [InlineData(data: ["Some.Thing.DTS-HD.MA.5.1", "Some Thing"])]
    [InlineData(data: ["Quiet.Place.TrueHD.Atmos", "Quiet Place"])]
    // flags
    [InlineData(data: ["Show.Title.MULTi.1080p", "Show Title"])]
    [InlineData(data: ["Name.REPACK.720p.HDTV", "Name"])]
    public void Strips_scene_tags(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected: expected);

    [Theory]
    // bare words that collide with tag substrings but must survive untouched
    [InlineData(data: ["Limitless", "Limitless"])]
    [InlineData(data: ["Web Therapy", "Web Therapy"])]
    [InlineData(data: ["Web of Lies", "Web of Lies"])]
    [InlineData(data: ["Extended Family", "Extended Family"])]
    [InlineData(data: ["Atomic Blonde", "Atomic Blonde"])]
    [InlineData(data: ["Atmosphere", "Atmosphere"])]
    [InlineData(data: ["Reacher", "Reacher"])]
    [InlineData(data: ["True Detective", "True Detective"])]
    [InlineData(data: ["Halt and Catch Fire", "Halt and Catch Fire"])]
    [InlineData(data: ["Money Heist", "Money Heist"])]
    [InlineData(data: ["The OA", "The OA"])]
    [InlineData(data: ["Multiplicity", "Multiplicity"])]
    [InlineData(data: ["The DVD Story", "The DVD Story"])]
    [InlineData(data: ["Camera Obscura", "Camera Obscura"])]
    [InlineData(data: ["The 4400", "The 4400"])]
    [InlineData(data: ["Apollo 13", "Apollo 13"])]
    [InlineData(data: ["Se7en", "Se7en"])]
    [InlineData(data: ["Blue Bloods", "Blue Bloods"])]
    // separator normalization is expected (dots/underscores become spaces)
    [InlineData(data: ["Mr.Robot", "Mr Robot"])]
    [InlineData(data: ["Spider-Man", "Spider-Man"])]
    [InlineData(data: ["X-Men", "X-Men"])]
    public void Preserves_real_titles(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected: expected);

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    public void Handles_empty(string input) =>
        input.CleanReleaseTitle().Should().BeEmpty();

    [Theory]
    // expanded scene vocabulary (source/streaming/codec/audio/hdr)
    [InlineData(data: ["Show.HDDVD.x264", "Show"])]
    [InlineData(data: ["Show.EDTV.XviD", "Show"])]
    [InlineData(data: ["Name.AMZN.WEB-DL.DDP5.1", "Name"])]
    [InlineData(data: ["Title.DSNP.WEBRip.x265.HDR", "Title"])]
    [InlineData(data: ["Movie.IMAX.1080p", "Movie"])]
    [InlineData(data: ["Show.HLG.x265", "Show"])]
    [InlineData(data: ["Film.VC1.AC3", "Film"])]
    [InlineData(data: ["Doc.MPEG2.PAL", "Doc"])]
    [InlineData(data: ["Thing.AVC.AAC", "Thing"])]
    [InlineData(data: ["Series.ATVP.WEB-DL.x264", "Series"])]
    public void Scene_vocab_strips(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected: expected);

    [Theory]
    // ambiguous scene FLAG words double as real titles and must survive
    [InlineData(data: ["Extended Family", "Extended Family"])]
    [InlineData(data: ["Final Space", "Final Space"])]
    [InlineData(data: ["Internal Affairs", "Internal Affairs"])]
    [InlineData(data: ["Anime Crimes Division", "Anime Crimes Division"])]
    [InlineData(data: ["WandaVision", "WandaVision"])]
    [InlineData(data: ["Vice", "Vice"])]
    [InlineData(data: ["Amazon", "Amazon"])]
    [InlineData(data: ["Imaximum", "Imaximum"])]
    public void Scene_vocab_preserves_real_titles(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected: expected);
}
