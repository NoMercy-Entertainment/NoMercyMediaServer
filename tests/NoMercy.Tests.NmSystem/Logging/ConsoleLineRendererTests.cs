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

using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging;
using NoMercy.NmSystem.Logging.Rendering;
using NoMercy.NmSystem.Text;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="ConsoleLineRenderer"/>: column alignment, the level marker slot,
/// the hanging gutter for continuation lines, the dim scope suffix, and that colour
/// escapes add zero display width (so alignment is identical coloured or plain).
/// </summary>
[Trait("Category", "Unit")]
public class ConsoleLineRendererTests
{
    private static readonly DateTime At = new(2026, 6, 29, 14, 23, 7, DateTimeKind.Utc);

    [Fact]
    public void Render_Plain_SingleLine_IsAligned()
    {
        string line = ConsoleLineRenderer.Render(
            At,
            LogLevel.Information,
            LogCategories.Resolve("moviedb"),
            "Fetching \"Inception\"",
            null,
            null,
            NoMercyConsoleTheme.Dark,
            color: false
        );

        line.Should().Be("14:23:07       TheMovieDB │ Fetching \"Inception\"");
    }

    [Fact]
    public void Render_Warning_KeepsCategoryColumnAligned()
    {
        string info = ConsoleLineRenderer.Render(
            At, LogLevel.Information, LogCategories.Resolve("moviedb"), "x", null, null,
            NoMercyConsoleTheme.Dark, color: false
        );
        string warn = ConsoleLineRenderer.Render(
            At, LogLevel.Warning, LogCategories.Resolve("moviedb"), "x", null, null,
            NoMercyConsoleTheme.Dark, color: false
        );

        warn.Should().Contain("!");
        info.IndexOf("TheMovieDB", StringComparison.Ordinal)
            .Should()
            .Be(warn.IndexOf("TheMovieDB", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MultiLineMessage_HangsUnderGutter()
    {
        string block = ConsoleLineRenderer.Render(
            At, LogLevel.Information, LogCategories.Resolve("moviedb"), "first\nsecond", null, null,
            NoMercyConsoleTheme.Dark, color: false
        );

        string[] lines = block.Split('\n');
        lines.Should().HaveCount(2);
        lines[1].Should().Be(new string(' ', 26) + "│ second");
    }

    [Fact]
    public void Render_Scope_AppendsDimSuffix()
    {
        string line = ConsoleLineRenderer.Render(
            At, LogLevel.Information, LogCategories.Resolve("moviedb"), "msg", "imp=7f3a", null,
            NoMercyConsoleTheme.Dark, color: false
        );

        line.Should().EndWith("· imp=7f3a");
    }

    [Fact]
    public void Render_Coloured_HasSameDisplayWidthAsPlain()
    {
        string plain = ConsoleLineRenderer.Render(
            At, LogLevel.Warning, LogCategories.Resolve("musicbrainz"), "Rate limit 429", null, null,
            NoMercyConsoleTheme.Dark, color: false
        );
        string coloured = ConsoleLineRenderer.Render(
            At, LogLevel.Warning, LogCategories.Resolve("musicbrainz"), "Rate limit 429", null, null,
            NoMercyConsoleTheme.Dark, color: true
        );

        DisplayWidth.Of(coloured).Should().Be(DisplayWidth.Of(plain));
    }
}
