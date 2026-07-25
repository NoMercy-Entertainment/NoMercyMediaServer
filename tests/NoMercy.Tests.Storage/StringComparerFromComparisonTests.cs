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

using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="StringComparerFromComparison"/> backs <see cref="StoragePathGuard"/>'s
/// root de-duplication (<c>Distinct(StringComparerFromComparison.For(...))</c>).
/// <see cref="StoragePathGuard"/>'s own constructor only ever picks one
/// <see cref="StringComparison"/> per OS, so exercising every switch arm
/// requires calling <c>For</c> directly — otherwise the five comparisons this
/// codebase never happens to construct with stay permanently unreachable.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StringComparerFromComparisonTests
{
    [Theory]
    [InlineData([StringComparison.Ordinal, "abc", "ABC", false])]
    [InlineData([StringComparison.OrdinalIgnoreCase, "abc", "ABC", true])]
    [InlineData([StringComparison.CurrentCulture, "abc", "ABC", false])]
    [InlineData([StringComparison.CurrentCultureIgnoreCase, "abc", "ABC", true])]
    [InlineData([StringComparison.InvariantCulture, "abc", "ABC", false])]
    [InlineData([StringComparison.InvariantCultureIgnoreCase, "abc", "ABC", true])]
    public void For_returns_a_comparer_matching_the_requested_comparison_semantics(
        StringComparison comparison,
        string left,
        string right,
        bool expectedEqual
    )
    {
        StringComparer comparer = StringComparerFromComparison.For(comparison);

        comparer.Equals(left, right).Should().Be(expectedEqual);
    }

    [Fact]
    public void For_falls_back_to_Ordinal_for_an_unrecognized_comparison_value()
    {
        StringComparer comparer = StringComparerFromComparison.For((StringComparison)999);

        comparer
            .Equals("abc", "ABC")
            .Should()
            .BeFalse("the default arm must behave like Ordinal, not silently ignore case");
    }
}
