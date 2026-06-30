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
using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Corpus for <see cref="StringExtensions.CleanSeriesTitle"/>: scene tags AND a
/// trailing release year are removed, but a year that IS the title (1883/1923)
/// is preserved.
/// </summary>
public class CleanSeriesTitleTests
{
    [Theory]
    [InlineData("Halo 2022", "Halo")]
    [InlineData("New Amsterdam 2018", "New Amsterdam")]
    [InlineData("Dark 2017", "Dark")]
    [InlineData("Halo 2022 1080p WEB", "Halo")]
    [InlineData("Breaking Bad", "Breaking Bad")]
    [InlineData("Stranger Things", "Stranger Things")]
    public void Strips_tags_and_trailing_year(string input, string expected) =>
        input.CleanSeriesTitle().Should().Be(expected);

    [Theory]
    // year-named shows: a leading year is the title, keep it
    [InlineData("1883", "1883")]
    [InlineData("1923", "1923")]
    [InlineData("1899", "1899")]
    public void Preserves_year_named_shows(string input, string expected) =>
        input.CleanSeriesTitle().Should().Be(expected);
}
