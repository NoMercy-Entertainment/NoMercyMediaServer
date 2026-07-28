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

using Moq;
using NoMercy.Database.Models.Libraries;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// Which storage an encode reads its source through.
/// <para>
/// Getting this wrong does not fail loudly in one place — it refuses the source
/// file on the OUTPUT folder's path guard, so the error names the library root
/// while pointing at a file that was never supposed to be under it. An archive
/// queued out of an intake folder failed that way on every single item, before
/// ffmpeg was ever invoked.
/// </para>
/// <para>
/// The comparison is the delicate part: the folder record and the picker's
/// output do not agree on separators, Windows does not agree with either about
/// case, and a root that is a string prefix of a sibling folder is not a
/// parent of it.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class SourceStorageResolverTests
{
    private sealed class RecordingStorageFactory : IStorageFactory
    {
        public List<(Ulid FolderId, Ulid DriverId, string SubPath)> Calls { get; } = [];

        public IStorage For(Ulid folderId, Ulid driverId, string subPath)
        {
            Calls.Add((folderId, driverId, subPath));
            return Mock.Of<IStorage>();
        }

        public void Invalidate(Ulid folderId) { }

        public void InvalidateAll() { }
    }

    private static readonly Ulid FolderId = Ulid.NewUlid();
    private static readonly Ulid DriverId = Ulid.NewUlid();
    private static readonly Ulid SourceDriverId = Ulid.NewUlid();

    private static Folder LibraryFolder(string path) =>
        new()
        {
            Id = FolderId,
            DriverId = DriverId,
            Path = path,
        };

    [Fact]
    public void A_named_source_driver_wins_over_everything_else()
    {
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        IStorage resolved = SourceStorageResolver.Resolve(
            factory,
            SourceDriverId,
            @"/export/intake/show/ep01.mkv",
            LibraryFolder(@"\\nas\Media\Libraries\Anime"),
            destination
        );

        resolved.Should().NotBeSameAs(destination);
        factory.Calls.Should().ContainSingle();
        factory.Calls[0].Should().Be((SourceDriverId, SourceDriverId, string.Empty));
    }

    [Fact]
    public void A_source_already_inside_the_library_keeps_the_destination_storage()
    {
        // The common case. It must allocate nothing new — and it must stay
        // bounded to the folder it belongs to rather than widening the guard.
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        IStorage resolved = SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            @"\\nas\Media\Libraries\Anime\Bleach\S01E01.mkv",
            LibraryFolder(@"\\nas\Media\Libraries\Anime"),
            destination
        );

        resolved.Should().BeSameAs(destination);
        factory.Calls.Should().BeEmpty();
    }

    [Fact]
    public void A_local_source_outside_the_library_is_read_from_its_own_directory()
    {
        // The bug: this returned the destination storage, whose guard is scoped
        // to the library root, so the encode was refused its own source file.
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            @"J:\Anime\Download\Detective.Conan\Season 25\ep799.mkv",
            LibraryFolder(@"\\nas\Media\Libraries\Anime"),
            destination
        );

        factory.Calls.Should().ContainSingle();
        factory.Calls[0].FolderId.Should().Be(FolderId);
        factory.Calls[0].DriverId.Should().Be(DriverId);
        factory
            .Calls[0]
            .SubPath.Should()
            .Be(
                @"J:\Anime\Download\Detective.Conan\Season 25",
                "the guard has to admit the directory the file actually lives in"
            );
    }

    [Theory]
    // The folder record and the picker do not agree on separators...
    [InlineData(@"\\nas\Media\Libraries\Anime", @"\\nas/Media/Libraries/Anime/Bleach/S01E01.mkv")]
    [InlineData(@"//nas/Media/Libraries/Anime", @"\\nas\Media\Libraries\Anime\Bleach\S01E01.mkv")]
    // ...nor on case, and Windows does not care about it either...
    [InlineData(@"\\nas\Media\Libraries\Anime", @"\\NAS\media\libraries\ANIME\Bleach\S01E01.mkv")]
    // ...nor on whether the root carries a trailing separator.
    [InlineData(@"\\nas\Media\Libraries\Anime\", @"\\nas\Media\Libraries\Anime\Bleach\S01E01.mkv")]
    public void A_source_inside_the_library_is_recognised_however_the_path_is_spelled(
        string root,
        string file
    )
    {
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        IStorage resolved = SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            file,
            LibraryFolder(root),
            destination
        );

        resolved.Should().BeSameAs(destination, "a spelling difference is not a different folder");
    }

    [Theory]
    // A sibling whose name merely STARTS with the library's name is not inside
    // it. Comparing raw prefixes would read this as in-library and hand the
    // encode a guard that refuses its source.
    [InlineData(@"\\nas\Media\Libraries\Anime2\Bleach\S01E01.mkv")]
    [InlineData(@"\\nas\Media\Libraries\AnimeMovies\Bleach\S01E01.mkv")]
    public void A_sibling_folder_sharing_the_librarys_name_is_not_inside_it(string file)
    {
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            file,
            LibraryFolder(@"\\nas\Media\Libraries\Anime"),
            destination
        );

        factory.Calls.Should().ContainSingle("a sibling folder needs its own scope");
    }

    [Fact]
    public void A_library_folder_with_no_path_falls_back_rather_than_widening_the_guard()
    {
        // A folder row with no path cannot say what is inside it. Treating that
        // as "nothing is inside it" would build a storage rooted wherever the
        // source happens to be, on a folder record that is already broken.
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            @"J:\Anime\Download\show\ep01.mkv",
            LibraryFolder(string.Empty),
            destination
        );

        factory.Calls.Should().ContainSingle();
        factory.Calls[0].SubPath.Should().Be(@"J:\Anime\Download\show");
    }

    [Fact]
    public void An_input_that_is_only_a_file_name_keeps_the_destination_storage()
    {
        // Nothing to root a source storage at, and inventing one would mean
        // rooting it at the process working directory.
        RecordingStorageFactory factory = new();
        IStorage destination = Mock.Of<IStorage>();

        IStorage resolved = SourceStorageResolver.Resolve(
            factory,
            sourceDriverId: null,
            "ep01.mkv",
            LibraryFolder(@"\\nas\Media\Libraries\Anime"),
            destination
        );

        resolved.Should().BeSameAs(destination);
        factory.Calls.Should().BeEmpty();
    }

    [Fact]
    public void The_library_root_itself_is_not_treated_as_a_file_inside_the_library()
    {
        // Guards the "+ separator" in the comparison: without it a path equal
        // to the root would count as being under the root.
        SourceStorageResolver
            .IsUnderRoot(@"\\nas\Media\Libraries\Anime", @"\\nas\Media\Libraries\Anime")
            .Should()
            .BeFalse();
    }
}
