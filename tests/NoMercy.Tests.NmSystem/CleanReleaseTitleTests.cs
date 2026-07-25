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
    [InlineData(["Breaking.Bad.1080p.WEB-DL.x265-GROUP", "Breaking Bad"])]
    [InlineData(["Some.Show.720p.HDTV.x264-LOL", "Some Show"])]
    [InlineData(["Movie Name 2160p UHD BluRay REMUX HDR10 TrueHD Atmos-GRP", "Movie Name"])]
    [InlineData(["Daredevil 1080i", "Daredevil"])]
    [InlineData(["Blade Runner 2049 4K", "Blade Runner 2049"])]
    // source
    [InlineData(["The.Title.WEBRip.DDP5.1.x264", "The Title"])]
    [InlineData(["Show Name WEB-DL", "Show Name"])]
    [InlineData(["Cool.Show.BluRay", "Cool Show"])]
    [InlineData(["Some Movie DVDRip XviD", "Some Movie"])]
    [InlineData(["Other Film HDTV", "Other Film"])]
    // codec / audio / hdr
    [InlineData(["Film.H.264.AAC", "Film"])]
    [InlineData(["Title.HEVC.10bit", "Title"])]
    [InlineData(["A.B.C.HDR.x265", "A B C"])]
    [InlineData(["Some.Thing.DTS-HD.MA.5.1", "Some Thing"])]
    [InlineData(["Quiet.Place.TrueHD.Atmos", "Quiet Place"])]
    // flags
    [InlineData(["Show.Title.MULTi.1080p", "Show Title"])]
    [InlineData(["Name.REPACK.720p.HDTV", "Name"])]
    public void Strips_scene_tags(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected);

    [Theory]
    // bare words that collide with tag substrings but must survive untouched
    [InlineData(["Limitless", "Limitless"])]
    [InlineData(["Web Therapy", "Web Therapy"])]
    [InlineData(["Web of Lies", "Web of Lies"])]
    [InlineData(["Extended Family", "Extended Family"])]
    [InlineData(["Atomic Blonde", "Atomic Blonde"])]
    [InlineData(["Atmosphere", "Atmosphere"])]
    [InlineData(["Reacher", "Reacher"])]
    [InlineData(["True Detective", "True Detective"])]
    [InlineData(["Halt and Catch Fire", "Halt and Catch Fire"])]
    [InlineData(["Money Heist", "Money Heist"])]
    [InlineData(["The OA", "The OA"])]
    [InlineData(["Multiplicity", "Multiplicity"])]
    [InlineData(["The DVD Story", "The DVD Story"])]
    [InlineData(["Camera Obscura", "Camera Obscura"])]
    [InlineData(["The 4400", "The 4400"])]
    [InlineData(["Apollo 13", "Apollo 13"])]
    [InlineData(["Se7en", "Se7en"])]
    [InlineData(["Blue Bloods", "Blue Bloods"])]
    // separator normalization is expected (dots/underscores become spaces)
    [InlineData(["Mr.Robot", "Mr Robot"])]
    [InlineData(["Spider-Man", "Spider-Man"])]
    [InlineData(["X-Men", "X-Men"])]
    public void Preserves_real_titles(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Handles_empty(string input) =>
        input.CleanReleaseTitle().Should().BeEmpty();

    [Theory]
    // expanded scene vocabulary (source/streaming/codec/audio/hdr)
    [InlineData(["Show.HDDVD.x264", "Show"])]
    [InlineData(["Show.EDTV.XviD", "Show"])]
    [InlineData(["Name.AMZN.WEB-DL.DDP5.1", "Name"])]
    [InlineData(["Title.DSNP.WEBRip.x265.HDR", "Title"])]
    [InlineData(["Movie.IMAX.1080p", "Movie"])]
    [InlineData(["Show.HLG.x265", "Show"])]
    [InlineData(["Film.VC1.AC3", "Film"])]
    [InlineData(["Doc.MPEG2.PAL", "Doc"])]
    [InlineData(["Thing.AVC.AAC", "Thing"])]
    [InlineData(["Series.ATVP.WEB-DL.x264", "Series"])]
    public void Scene_vocab_strips(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected);

    [Theory]
    // ambiguous scene FLAG words double as real titles and must survive
    [InlineData(["Extended Family", "Extended Family"])]
    [InlineData(["Final Space", "Final Space"])]
    [InlineData(["Internal Affairs", "Internal Affairs"])]
    [InlineData(["Anime Crimes Division", "Anime Crimes Division"])]
    [InlineData(["WandaVision", "WandaVision"])]
    [InlineData(["Vice", "Vice"])]
    [InlineData(["Amazon", "Amazon"])]
    [InlineData(["Imaximum", "Imaximum"])]
    public void Scene_vocab_preserves_real_titles(string input, string expected) =>
        input.CleanReleaseTitle().Should().Be(expected);
}
