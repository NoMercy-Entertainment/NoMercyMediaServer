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

using NoMercy.MediaProcessing.Files.Parsing;

namespace NoMercy.Tests.MediaProcessing.Parsing;

/// <summary>
/// Locks the music album-path parser against the behaviour of the original
/// inlined regex across the supported library layouts (letter / [type] folders,
/// [year] and [Singles] releases, Windows and POSIX separators, and the
/// no-match folder-name fallback).
/// </summary>
public class MusicPathParserTests
{
    [Theory]
    [InlineData(data: ["/data/Music/A/Adele/[2015] 25", 2015, "25", "Adele", null])]
    [InlineData(data: ["/data/Music/[Albums]/Daft Punk/[2013] Random Access Memories", 2013, "Random Access Memories", "Daft Punk", null])]
    [InlineData(data: [@"D:\Music\R\Radiohead\[1997] OK Computer", 1997, "OK Computer", "Radiohead", null])]
    [InlineData(data: ["/m/Music/T/Taylor Swift/[Singles] Cardigan", 0, "Cardigan", "Taylor Swift", "Singles"])]
    [InlineData(data: ["/srv/Music/M/Miles Davis/[1959] Kind of Blue", 1959, "Kind of Blue", "Miles Davis", null])]
    public void Parses_album_layouts(
        string path,
        int year,
        string album,
        string? artist,
        string? releaseType
    )
    {
        MusicPathParser.MusicAlbumInfo info = MusicPathParser.Parse(directoryPath: path, folderName: "ignored");
        info.Year.Should().Be(expected: year);
        info.AlbumName.Should().Be(expected: album);
        info.Artist.Should().Be(expected: artist);
        info.ReleaseType.Should().Be(expected: releaseType);
    }

    [Theory]
    // unmatched path -> album falls back to the folder name (minus a [year] tag)
    [InlineData(data: ["/m/Music/Adele", "Adele", "Adele"])]
    [InlineData(data: ["not-a-library-path", "[2020] Best Of", "Best Of"])]
    [InlineData(data: ["flat", "[1991] Nevermind", "Nevermind"])]
    public void Falls_back_to_folder_name(string path, string folder, string expectedAlbum)
    {
        MusicPathParser.MusicAlbumInfo info = MusicPathParser.Parse(directoryPath: path, folderName: folder);
        info.Year.Should().Be(expected: 0);
        info.AlbumName.Should().Be(expected: expectedAlbum);
    }
}
