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
    [InlineData(data: ["a/b/c.mkv", "c.mkv"])]
    [InlineData(data: ["just-a-name.mkv", "just-a-name.mkv"])]
    [InlineData(data: ["a/b/", "b"])]
    [InlineData(data: ["a/", "a"])]
    [InlineData(data: ["/leading/file", "file"])]
    public void GetName_ReturnsLastSegment(string input, string expected)
    {
        StoragePathHelpers.GetName(path: input).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: null)]
    public void GetName_EmptyOrNull_ReturnsEmpty(string? input)
    {
        StoragePathHelpers.GetName(path: input!).Should().Be(expected: string.Empty);
    }

    [Fact]
    public void GetName_BackslashOnlyPath_RequiresCallerToNormalizeFirst()
    {
        // Rule 2: GetName splits on '/' only, by design. A raw UNC/Windows
        // path handed in without normalizing separators first returns the
        // whole string unchanged instead of the trailing segment — this is
        // exactly what let Track.Filename get stored as
        // "/\\192.168.2.120\mnt\vault\Media\...\track.mp3" instead of
        // "/track.mp3" (RecordingManager.Store, MusicLogic.StoreTrack) before
        // those call sites normalized backslashes to '/' first.
        string uncPath = "\\\\192.168.2.120\\mnt\\vault\\Media\\track.mp3";
        StoragePathHelpers.GetName(path: uncPath).Should().Be(expected: uncPath);
        StoragePathHelpers.GetName(path: uncPath.Replace(oldChar: '\\', newChar: '/')).Should().Be(expected: "track.mp3");
    }

    // ── GetParent ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["a/b/c.mkv", "a/b"])]
    [InlineData(data: ["a/b", "a"])]
    [InlineData(data: ["a/b/c/", "a/b"])]
    public void GetParent_ReturnsParentSegment(string input, string expected)
    {
        StoragePathHelpers.GetParent(path: input).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: "solo.mkv")]
    [InlineData(data: "solo")]
    [InlineData(data: "")]
    public void GetParent_NoParent_ReturnsNull(string input)
    {
        StoragePathHelpers.GetParent(path: input).Should().BeNull();
    }

    [Fact]
    public void GetParent_TrailingSlashIsTrimmedBeforeSplit()
    {
        // "a/" trims to "a" — no parent.
        StoragePathHelpers.GetParent(path: "a/").Should().BeNull();
    }

    // ── GetNameWithoutExtension ────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["a/b/c.mkv", "c"])]
    [InlineData(data: ["c.mkv", "c"])]
    [InlineData(data: ["multi.dot.name.mkv", "multi.dot.name"])]
    [InlineData(data: ["noext", "noext"])]
    [InlineData(data: ["a/b/noext", "noext"])]
    public void GetNameWithoutExtension_StripsLastDotSegment(string input, string expected)
    {
        StoragePathHelpers.GetNameWithoutExtension(path: input).Should().Be(expected: expected);
    }

    [Fact]
    public void GetNameWithoutExtension_LeadingDotFile_TreatedAsExtension()
    {
        // ".bashrc" - the LastIndexOf('.') returns 0, so the slice is
        // empty. Documents the current behaviour: dotfile-style names
        // map to empty. Consumers should not rely on this for hidden-file
        // semantics — IStorage paths are forward-slash relative, not
        // POSIX-flavored.
        StoragePathHelpers.GetNameWithoutExtension(path: ".bashrc").Should().BeEmpty();
    }

    // ── Combine ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["a", "b", "a/b"])]
    [InlineData(data: ["a/", "b", "a/b"])]
    [InlineData(data: ["a", "/b", "a/b"])]
    [InlineData(data: ["a/", "/b", "a/b"])]
    [InlineData(data: ["a/b", "c/d", "a/b/c/d"])]
    public void Combine_JoinsWithSingleSlash(string parent, string child, string expected)
    {
        StoragePathHelpers.Combine(parent: parent, child: child).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["", "child", "child"])]
    [InlineData(data: ["parent", "", "parent"])]
    [InlineData(data: ["", "", ""])]
    public void Combine_EmptyOperand_ReturnsOtherUnchanged(
        string parent,
        string child,
        string expected
    )
    {
        StoragePathHelpers.Combine(parent: parent, child: child).Should().Be(expected: expected);
    }

    [Fact]
    public void Combine_StripsBackslashesFromBothEnds()
    {
        // Windows-style backslash on the parent or leading-slash on the
        // child must not survive the join. The helper is the safety net
        // when something accidentally hands a Windows-flavored fragment
        // into a storage-relative path.
        StoragePathHelpers.Combine(parent: "a\\", child: "b").Should().Be(expected: "a/b");
        StoragePathHelpers.Combine(parent: "a", child: "\\b").Should().Be(expected: "a/b");
        StoragePathHelpers.Combine(parent: "a\\", child: "\\b").Should().Be(expected: "a/b");
    }
}
