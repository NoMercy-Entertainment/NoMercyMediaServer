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

namespace NoMercy.Tests.Storage;

/// <summary>
/// Pure-function tests for StoragePathHelpers. These helpers are the
/// no-IStorage-in-scope fallback used across the codebase wherever
/// generator or analyzer code needs to manipulate forward-slash paths
/// without taking a dependency. Every consumer trusts these to split
/// on '/' (Rule 2 of the IStorage path contract), never on the OS
/// separator. A regression here would silently mis-parse Windows paths.
/// </summary>
public class StoragePathHelpersTests
{
    // ── GetName ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a/b/c.mkv", "c.mkv")]
    [InlineData("just-a-name.mkv", "just-a-name.mkv")]
    [InlineData("a/b/", "b")]
    [InlineData("a/", "a")]
    [InlineData("/leading/file", "file")]
    public void GetName_ReturnsLastSegment(string input, string expected)
    {
        StoragePathHelpers.GetName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetName_EmptyOrNull_ReturnsEmpty(string? input)
    {
        StoragePathHelpers.GetName(input!).Should().Be(string.Empty);
    }

    // ── GetParent ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a/b/c.mkv", "a/b")]
    [InlineData("a/b", "a")]
    [InlineData("a/b/c/", "a/b")]
    public void GetParent_ReturnsParentSegment(string input, string expected)
    {
        StoragePathHelpers.GetParent(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("solo.mkv")]
    [InlineData("solo")]
    [InlineData("")]
    public void GetParent_NoParent_ReturnsNull(string input)
    {
        StoragePathHelpers.GetParent(input).Should().BeNull();
    }

    [Fact]
    public void GetParent_TrailingSlashIsTrimmedBeforeSplit()
    {
        // "a/" trims to "a" — no parent.
        StoragePathHelpers.GetParent("a/").Should().BeNull();
    }

    // ── GetNameWithoutExtension ────────────────────────────────────────────

    [Theory]
    [InlineData("a/b/c.mkv", "c")]
    [InlineData("c.mkv", "c")]
    [InlineData("multi.dot.name.mkv", "multi.dot.name")]
    [InlineData("noext", "noext")]
    [InlineData("a/b/noext", "noext")]
    public void GetNameWithoutExtension_StripsLastDotSegment(string input, string expected)
    {
        StoragePathHelpers.GetNameWithoutExtension(input).Should().Be(expected);
    }

    [Fact]
    public void GetNameWithoutExtension_LeadingDotFile_TreatedAsExtension()
    {
        // ".bashrc" - the LastIndexOf('.') returns 0, so the slice is
        // empty. Documents the current behaviour: dotfile-style names
        // map to empty. Consumers should not rely on this for hidden-file
        // semantics — IStorage paths are forward-slash relative, not
        // POSIX-flavored.
        StoragePathHelpers.GetNameWithoutExtension(".bashrc").Should().BeEmpty();
    }

    // ── Combine ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a", "b", "a/b")]
    [InlineData("a/", "b", "a/b")]
    [InlineData("a", "/b", "a/b")]
    [InlineData("a/", "/b", "a/b")]
    [InlineData("a/b", "c/d", "a/b/c/d")]
    public void Combine_JoinsWithSingleSlash(string parent, string child, string expected)
    {
        StoragePathHelpers.Combine(parent, child).Should().Be(expected);
    }

    [Theory]
    [InlineData("", "child", "child")]
    [InlineData("parent", "", "parent")]
    [InlineData("", "", "")]
    public void Combine_EmptyOperand_ReturnsOtherUnchanged(
        string parent,
        string child,
        string expected
    )
    {
        StoragePathHelpers.Combine(parent, child).Should().Be(expected);
    }

    [Fact]
    public void Combine_StripsBackslashesFromBothEnds()
    {
        // Windows-style backslash on the parent or leading-slash on the
        // child must not survive the join. The helper is the safety net
        // when something accidentally hands a Windows-flavored fragment
        // into a storage-relative path.
        StoragePathHelpers.Combine("a\\", "b").Should().Be("a/b");
        StoragePathHelpers.Combine("a", "\\b").Should().Be("a/b");
        StoragePathHelpers.Combine("a\\", "\\b").Should().Be("a/b");
    }
}
