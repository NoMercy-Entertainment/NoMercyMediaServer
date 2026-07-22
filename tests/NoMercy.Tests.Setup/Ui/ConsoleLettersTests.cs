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

using NoMercy.Setup.Ui;

namespace NoMercy.Tests.Setup.Ui;

/// <summary>
/// Requirement: <see cref="ConsoleMessages.Logo"/> renders the banner text
/// "NoMercy MediaServer  ║" one character at a time by looking each character up in
/// <see cref="ConsoleLetters.Colossal"/> (or <see cref="ConsoleLetters.ColossalXmas"/>
/// during the Christmas window) and indexing every row up to <c>Count - 1</c>. Both
/// glyph tables must therefore: contain every character that string can ever produce,
/// have every glyph the exact same row count (Logo indexes rows positionally across
/// different characters in the same line), and never contain a null or missing row —
/// a gap here is a runtime <c>KeyNotFoundException</c> or <c>IndexOutOfRangeException</c>
/// during the very first thing a fresh install prints to the console.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ConsoleLettersTests
{
    // The exact banner text ConsoleMessages.Logo renders, character by character.
    private const string BannerText = "║  NoMercy MediaServer  ║";

    [Theory]
    [InlineData(data: nameof(ConsoleLetters.Colossal))]
    [InlineData(data: nameof(ConsoleLetters.ColossalXmas))]
    public void Table_ContainsEveryCharacterTheBannerNeeds(string tableName)
    {
        Dictionary<string, List<string>> table = GetTable(name: tableName);

        foreach (char c in BannerText.Distinct())
        {
            Assert.True(
                condition: table.ContainsKey(key: c.ToString()),
                userMessage: $"{tableName} is missing a glyph for '{c}' — Logo() would throw a KeyNotFoundException"
            );
        }
    }

    [Theory]
    [InlineData(data: nameof(ConsoleLetters.Colossal))]
    [InlineData(data: nameof(ConsoleLetters.ColossalXmas))]
    public void Table_EveryGlyphHasTheSameRowCount(string tableName)
    {
        Dictionary<string, List<string>> table = GetTable(name: tableName);

        int expectedRows = table.Values.First().Count;

        foreach ((string letter, List<string> rows) in table)
        {
            Assert.Equal(expected: expectedRows, actual: rows.Count);
        }
    }

    [Theory]
    [InlineData(data: nameof(ConsoleLetters.Colossal))]
    [InlineData(data: nameof(ConsoleLetters.ColossalXmas))]
    public void Table_NoRowIsNullOrMissing(string tableName)
    {
        Dictionary<string, List<string>> table = GetTable(name: tableName);

        foreach ((string letter, List<string> rows) in table)
        {
            Assert.All(collection: rows, action: row => Assert.NotNull(@object: row));
        }
    }

    [Theory]
    [InlineData(data: nameof(ConsoleLetters.Colossal))]
    [InlineData(data: nameof(ConsoleLetters.ColossalXmas))]
    public void Table_PipeGlyph_IsUsedAsTheBorderCharacter(string tableName)
    {
        // Logo() special-cases '║' with Colors[0] specifically because it renders the
        // box border — the glyph must exist and render as a single repeated character
        // (its row count must still match every other glyph per the test above).
        Dictionary<string, List<string>> table = GetTable(name: tableName);

        Assert.True(condition: table.ContainsKey(key: "║"));
        Assert.All(collection: table[key: "║"], action: row => Assert.Equal(expected: "║", actual: row));
    }

    [Fact]
    public void Logo_RowCountsMatch_AcrossBothTables()
    {
        // Logo() picks one table or the other at runtime based on the calendar (Xmas
        // window) — both must present the same "row count - 1" iteration bound, or
        // switching tables mid-way through a run (date rollover) would change the
        // rendered banner's height unexpectedly.
        Assert.Equal(
            expected: ConsoleLetters.Colossal.Values.First().Count,
            actual: ConsoleLetters.ColossalXmas.Values.First().Count
        );
    }

    [Theory]
    [InlineData(data: nameof(ConsoleLetters.Colossal))]
    [InlineData(data: nameof(ConsoleLetters.ColossalXmas))]
    public void Table_LettersUsedForBrandColor_Exist(string tableName)
    {
        // Logo()'s Colors[1] switch case explicitly names 'N', 'M', 'S' (from "NoMercy
        // MediaServer") as the brand-highlighted letters — they must exist in the table
        // the same way any other rendered character must.
        Dictionary<string, List<string>> table = GetTable(name: tableName);

        foreach (char brand in "NMS")
        {
            Assert.True(condition: table.ContainsKey(key: brand.ToString()));
        }
    }

    [Fact]
    public void Colossal_AndColossalXmas_AreDistinctTableInstances()
    {
        Assert.NotSame(expected: ConsoleLetters.Colossal, actual: ConsoleLetters.ColossalXmas);
    }

    private static Dictionary<string, List<string>> GetTable(string name) =>
        name == nameof(ConsoleLetters.Colossal)
            ? ConsoleLetters.Colossal
            : ConsoleLetters.ColossalXmas;
}
