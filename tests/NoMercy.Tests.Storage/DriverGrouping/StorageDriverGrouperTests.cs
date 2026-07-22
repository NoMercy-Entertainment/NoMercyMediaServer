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

[Trait(name: "Category", value: "Unit")]
public class StorageDriverGrouperTests
{
    // -----------------------------------------------------------------------
    // DetectEndpoint
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: [@"\\192.168.1.1\Media\Anime", @"\\192.168.1.1\Media", StorageEndpointKind.Smb])]
    [InlineData(data: [@"\\192.168.1.1\Media\Movies", @"\\192.168.1.1\Media", StorageEndpointKind.Smb])]
    [InlineData(data: [@"\\NAS\Share\Sub\Folder", @"\\NAS\Share", StorageEndpointKind.Smb])]
    [InlineData(data: [@"\\server\share", @"\\server\share", StorageEndpointKind.Smb])]
    public void DetectEndpoint_UncPath_ReturnsSmbEndpoint(
        string path,
        string expectedKey,
        StorageEndpointKind expectedKind
    )
    {
        StorageEndpoint endpoint = StorageDriverGrouper.DetectEndpoint(absolutePath: path);

        endpoint.Key.Should().Be(expected: expectedKey);
        endpoint.Kind.Should().Be(expected: expectedKind);
    }

    [Theory]
    [InlineData(data: [@"C:\Media\Movies", "C:", StorageEndpointKind.Local])]
    [InlineData(data: [@"D:\NAS\Anime", "D:", StorageEndpointKind.Local])]
    [InlineData(data: [@"E:\", "E:", StorageEndpointKind.Local])]
    public void DetectEndpoint_WindowsDrivePath_ReturnsLocalEndpoint(
        string path,
        string expectedKey,
        StorageEndpointKind expectedKind
    )
    {
        StorageEndpoint endpoint = StorageDriverGrouper.DetectEndpoint(absolutePath: path);

        endpoint.Key.Should().Be(expected: expectedKey);
        endpoint.Kind.Should().Be(expected: expectedKind);
    }

    [Fact]
    public void DetectEndpoint_PosixPath_ReturnsLocalEndpointWithSlashKey()
    {
        StorageEndpoint endpoint = StorageDriverGrouper.DetectEndpoint(absolutePath: "/mnt/nas/media");

        endpoint.Key.Should().Be(expected: "/");
        endpoint.Kind.Should().Be(expected: StorageEndpointKind.Local);
    }

    // -----------------------------------------------------------------------
    // ComputeCommonAncestor
    // -----------------------------------------------------------------------

    [Fact]
    public void ComputeCommonAncestor_SinglePath_ReturnsThatPath()
    {
        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: [@"C:\Media\Movies"],
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: @"C:\Media\Movies");
    }

    [Fact]
    public void ComputeCommonAncestor_TwoSiblingPaths_ReturnsSharedParent()
    {
        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: [@"C:\Media\Movies", @"C:\Media\TV"],
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: @"C:\Media");
    }

    [Fact]
    public void ComputeCommonAncestor_FourUncPaths_ReturnsUncShare()
    {
        List<string> paths =
        [
            @"\\192.168.1.1\Media\Anime",
            @"\\192.168.1.1\Media\Movies",
            @"\\192.168.1.1\Media\TV",
            @"\\192.168.1.1\Media\Music",
        ];

        string result = StorageDriverGrouper.ComputeCommonAncestor(absolutePaths: paths, kind: StorageEndpointKind.Smb);

        result.Should().Be(expected: @"\\192.168.1.1\Media");
    }

    [Fact]
    public void ComputeCommonAncestor_NestedPaths_ReturnsDeepestCommonAncestor()
    {
        List<string> paths =
        [
            @"C:\Media\TV\Anime",
            @"C:\Media\TV\Western",
            @"C:\Media\TV\Anime\2024",
        ];

        string result = StorageDriverGrouper.ComputeCommonAncestor(
            absolutePaths: paths,
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: @"C:\Media\TV");
    }

    // -----------------------------------------------------------------------
    // ComputeSubPath
    // -----------------------------------------------------------------------

    [Fact]
    public void ComputeSubPath_FolderEqualsRoot_ReturnsEmpty()
    {
        string result = StorageDriverGrouper.ComputeSubPath(
            driverRoot: @"C:\Media\Anime",
            absolutePath: @"C:\Media\Anime",
            kind: StorageEndpointKind.Local
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSubPath_FolderUnderRoot_ReturnsRelativeSegment()
    {
        string result = StorageDriverGrouper.ComputeSubPath(
            driverRoot: @"C:\Media",
            absolutePath: @"C:\Media\Anime",
            kind: StorageEndpointKind.Local
        );

        result.Should().Be(expected: "Anime");
    }

    [Fact]
    public void ComputeSubPath_UncFolderUnderShare_ReturnsRelativeName()
    {
        string result = StorageDriverGrouper.ComputeSubPath(
            driverRoot: @"\\192.168.1.1\Media",
            absolutePath: @"\\192.168.1.1\Media\Movies",
            kind: StorageEndpointKind.Smb
        );

        result.Should().Be(expected: "Movies");
    }

    // -----------------------------------------------------------------------
    // Group — primary scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public void Group_EmptyInput_ReturnsEmpty()
    {
        IReadOnlyList<DriverGroup> result = StorageDriverGrouper.Group(inputs: []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Group_SingleFolder_ProducesOneDriveRootedAtThatFolder()
    {
        Ulid folderId = Ulid.NewUlid();
        FolderRootInput[] inputs = [new(FolderId: folderId, AbsoluteRootPath: @"C:\Media\Movies")];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 1);
        DriverGroup group = groups[index: 0];
        group.DriverRoot.Should().Be(expected: @"C:\Media\Movies");
        group.DriverType.Should().Be(expected: "local");
        group.Folders.Should().ContainSingle(predicate: a => a.FolderId == folderId && a.SubPath == "");
    }

    [Fact]
    public void Group_FourUncFoldersUnderOneShare_ProducesOneLocalDriverWithFourSubPaths()
    {
        Ulid animeId = Ulid.NewUlid();
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();
        Ulid musicId = Ulid.NewUlid();

        FolderRootInput[] inputs =
        [
            new(FolderId: animeId, AbsoluteRootPath: @"\\192.168.1.1\Media\Anime"),
            new(FolderId: moviesId, AbsoluteRootPath: @"\\192.168.1.1\Media\Movies"),
            new(FolderId: tvId, AbsoluteRootPath: @"\\192.168.1.1\Media\TV"),
            new(FolderId: musicId, AbsoluteRootPath: @"\\192.168.1.1\Media\Music"),
        ];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 1);
        DriverGroup group = groups[index: 0];
        group.DriverType.Should().Be(expected: "local");
        group.DriverRoot.Should().Be(expected: @"\\192.168.1.1\Media");
        group.Folders.Should().HaveCount(expected: 4);
        group.Folders.Should().Contain(predicate: a => a.FolderId == animeId && a.SubPath == "Anime");
        group.Folders.Should().Contain(predicate: a => a.FolderId == moviesId && a.SubPath == "Movies");
        group.Folders.Should().Contain(predicate: a => a.FolderId == tvId && a.SubPath == "TV");
        group.Folders.Should().Contain(predicate: a => a.FolderId == musicId && a.SubPath == "Music");
    }

    [Fact]
    public void Group_TwoDifferentDrives_ProducesTwoLocalDrivers()
    {
        Ulid movieId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        FolderRootInput[] inputs = [new(FolderId: movieId, AbsoluteRootPath: @"C:\Media\Movies"), new(FolderId: tvId, AbsoluteRootPath: @"D:\Media\TV")];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 2);
        groups.Select(selector: g => g.DriverType).Should().AllBe(expectation: "local");
        groups.Should().Contain(predicate: g => g.Folders.Any(a => a.FolderId == movieId));
        groups.Should().Contain(predicate: g => g.Folders.Any(a => a.FolderId == tvId));
    }

    [Fact]
    public void Group_TwoDifferentUncShares_ProducesTwoDriversNotMerged()
    {
        Ulid movies1Id = Ulid.NewUlid();
        Ulid movies2Id = Ulid.NewUlid();

        FolderRootInput[] inputs =
        [
            new(FolderId: movies1Id, AbsoluteRootPath: @"\\nas1\Media\Movies"),
            new(FolderId: movies2Id, AbsoluteRootPath: @"\\nas2\Media\Movies"),
        ];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 2);
        groups.Select(selector: g => g.DriverType).Should().AllBe(expectation: "local");
        groups.Select(selector: g => g.DriverRoot).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Group_MixedLocalSubTreesUnderSameDrive_ProducesOneDriverWithLca()
    {
        Ulid animesId = Ulid.NewUlid();
        Ulid moviesId = Ulid.NewUlid();
        Ulid musicId = Ulid.NewUlid();

        FolderRootInput[] inputs =
        [
            new(FolderId: animesId, AbsoluteRootPath: @"C:\Data\Media\Anime"),
            new(FolderId: moviesId, AbsoluteRootPath: @"C:\Data\Media\Movies"),
            new(FolderId: musicId, AbsoluteRootPath: @"C:\Data\Media\Music"),
        ];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 1);
        DriverGroup group = groups[index: 0];
        group.DriverRoot.Should().Be(expected: @"C:\Data\Media");
        group.DriverType.Should().Be(expected: "local");
        group.Folders.Should().HaveCount(expected: 3);
        group.Folders.Should().Contain(predicate: a => a.FolderId == animesId && a.SubPath == "Anime");
        group.Folders.Should().Contain(predicate: a => a.FolderId == moviesId && a.SubPath == "Movies");
        group.Folders.Should().Contain(predicate: a => a.FolderId == musicId && a.SubPath == "Music");
    }

    [Fact]
    public void Group_LcaCorrectness_TwoPathsDifferingAtFirstSegment_RootsAtDrive()
    {
        Ulid aId = Ulid.NewUlid();
        Ulid bId = Ulid.NewUlid();

        FolderRootInput[] inputs = [new(FolderId: aId, AbsoluteRootPath: @"C:\Alpha\Movies"), new(FolderId: bId, AbsoluteRootPath: @"C:\Beta\TV")];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 1);
        DriverGroup group = groups[index: 0];
        group.DriverRoot.Should().Be(expected: @"C:\");
        group.Folders.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void Group_UncAndLocalFolders_NeverMergedAcrossEndpoints()
    {
        Ulid localId = Ulid.NewUlid();
        Ulid uncId = Ulid.NewUlid();

        FolderRootInput[] inputs =
        [
            new(FolderId: localId, AbsoluteRootPath: @"C:\Media\Movies"),
            new(FolderId: uncId, AbsoluteRootPath: @"\\nas1\Media\Movies"),
        ];

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        groups.Should().HaveCount(expected: 2);
        groups.Select(selector: g => g.DriverType).Should().AllBe(expectation: "local");
        groups.Should().Contain(predicate: g => g.DriverRoot.StartsWith(@"C:"));
        groups.Should().Contain(predicate: g => g.DriverRoot.StartsWith(@"\\nas1\Media"));
    }
}
