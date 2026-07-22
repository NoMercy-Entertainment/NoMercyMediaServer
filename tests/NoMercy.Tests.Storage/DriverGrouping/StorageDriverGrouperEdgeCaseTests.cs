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

using NoMercy.Storage.DriverGrouping;

namespace NoMercy.Tests.Storage.DriverGrouping;

/// <summary>
/// Edge cases of <see cref="StorageDriverGrouper"/>'s pure path-math helpers
/// that <see cref="StorageDriverGrouperTests"/> doesn't reach: a UNC path with
/// no share segment at all, an empty input list, two path sets with zero
/// common ancestor, a sub-path that isn't actually under its claimed root,
/// and POSIX-style (non-Windows-drive) segment joining.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class StorageDriverGrouperEdgeCaseTests
{
    [Fact]
    public void DetectEndpoint_UncPath_with_no_share_segment_uses_server_only_as_key()
    {
        // "\\server" with nothing after it — malformed/incomplete UNC input,
        // but DetectEndpoint must still classify it as SMB instead of throwing.
        StorageEndpoint endpoint = StorageDriverGrouper.DetectEndpoint(absolutePath: @"\\server");

        endpoint.Key.Should().Be(expected: @"\\server");
        endpoint.Kind.Should().Be(expected: StorageEndpointKind.Smb);
    }

    [Fact]
    public void ComputeCommonAncestor_throws_for_empty_path_list()
    {
        Action act = () =>
            StorageDriverGrouper.ComputeCommonAncestor(absolutePaths: [], kind: StorageEndpointKind.Local);

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*empty*");
    }

    [Fact]
    public void ComputeCommonAncestor_returns_root_marker_when_local_paths_share_no_segment()
    {
        // Different Windows drives share nothing in common — the ancestor
        // degrades to the local root marker rather than throwing.
        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: [@"C:\Foo\Bar", @"D:\Baz\Qux"],
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: "/");
    }

    [Fact]
    public void ComputeCommonAncestor_returns_smb_root_marker_when_smb_paths_share_no_segment()
    {
        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: [@"\\alpha\share\x", @"\\beta\share\y"],
            kind: StorageEndpointKind.Smb
        );

        result.Should().Be(expected: @"\\");
    }

    [Fact]
    public void ComputeSubPath_returns_the_full_path_when_it_is_not_under_the_claimed_root()
    {
        // A caller-supplied "root" that isn't actually an ancestor of the path
        // must not silently truncate — the full path is the honest answer.
        string result = StorageDriverGrouper.ComputeSubPath(
            driverRoot: @"C:\Media\Movies",
            absolutePath: @"C:\Downloads\Movie.mkv",
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: @"C:\Downloads\Movie.mkv");
    }

    [Fact]
    public void ComputeCommonAncestor_joins_posix_style_paths_without_a_drive_letter()
    {
        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: ["/mnt/media/movies", "/mnt/media/tv"],
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: "/mnt/media");
    }
}
