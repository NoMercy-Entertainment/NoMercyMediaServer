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
/// the hanging gutter for continuation lines, and that colour escapes add zero
/// display width (so alignment is identical coloured or plain).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ConsoleLineRendererTests
{
    private static readonly DateTime At = new(year: 2026, month: 6, day: 29, hour: 14, minute: 23, second: 7, kind: DateTimeKind.Utc);

    [Fact]
    public void Render_Plain_SingleLine_IsAligned()
    {
        string line = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Information,
            category: LogCategories.Resolve(key: "moviedb"),
            message: "Fetching \"Inception\"",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: false
        );

        line.Should().Be(expected: "14:23:07       TheMovieDB │ Fetching \"Inception\"");
    }

    [Fact]
    public void Render_Warning_KeepsCategoryColumnAligned()
    {
        string info = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Information,
            category: LogCategories.Resolve(key: "moviedb"),
            message: "x",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: false
        );
        string warn = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Warning,
            category: LogCategories.Resolve(key: "moviedb"),
            message: "x",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: false
        );

        warn.Should().Contain(expected: "!");
        info.IndexOf(value: "TheMovieDB", comparisonType: StringComparison.Ordinal)
            .Should()
            .Be(expected: warn.IndexOf(value: "TheMovieDB", comparisonType: StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MultiLineMessage_HangsUnderGutter()
    {
        string block = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Information,
            category: LogCategories.Resolve(key: "moviedb"),
            message: "first\nsecond",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: false
        );

        string[] lines = block.Split(separator: '\n');
        lines.Should().HaveCount(expected: 2);
        lines[1].Should().Be(expected: new string(c: ' ', count: 26) + "│ second");
    }

    [Fact]
    public void Render_Coloured_HasSameDisplayWidthAsPlain()
    {
        string plain = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Warning,
            category: LogCategories.Resolve(key: "musicbrainz"),
            message: "Rate limit 429",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: false
        );
        string coloured = ConsoleLineRenderer.Render(
            timestamp: At,
            level: LogLevel.Warning,
            category: LogCategories.Resolve(key: "musicbrainz"),
            message: "Rate limit 429",
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: true
        );

        DisplayWidth.Of(text: coloured).Should().Be(expected: DisplayWidth.Of(text: plain));
    }
}
